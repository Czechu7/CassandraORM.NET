using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cassandra;
using CassandraOrm.Core;
using CassandraOrm.Configuration;
using CassandraOrm.Mapping;
using CassandraOrm.UDT;
using CassandraOrm.MaterializedViews;
using CassandraOrm.Collections;
using Microsoft.Extensions.Logging;
using CassandraOrm.Extensions;

namespace CassandraOrm.Examples;

/// <summary>
/// Real-world example demonstrating CassandraORM.NET library capabilities
/// This example shows a social media platform with users, posts, comments, and analytics
/// </summary>
public class RealWorldExample
{
    public static async Task Main(string[] args)
    {
        // Configure logging
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<RealWorldExample>();

        // Configure Cassandra connection
        var config = new CassandraConfiguration
        {
            ContactPoints = new[] { "127.0.0.1" },
            Port = 9042,
            Keyspace = "social_media",
            AutoCreateKeyspace = true,
            ReplicationFactor = 1,
            ConnectionTimeout = 30000,
            QueryTimeout = 30000
        };

        // Create and initialize the context
        using var context = new SocialMediaContext(config, logger);
        
        try
        {
            // Initialize database schema
            await context.EnsureCreatedAsync();
            await context.CreateViewsAsync();

            // Run examples
            await UserManagementExample(context, logger);
            await PostAndCommentExample(context, logger);
            await MaterializedViewExample(context, logger);
            await AnalyticsExample(context, logger);
            await AdvancedQueryExample(context, logger);

            logger.LogInformation("Example completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred: {Message}", ex.Message);
        }
    }

    #region Entity Models

    // User-defined types for complex data structures
    [UserDefinedType("user_profile")]
    public class UserProfile
    {
        public string Bio { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime JoinDate { get; set; }
    }

    [UserDefinedType("post_metadata")]
    public class PostMetadata
    {
        public int ReadTimeMinutes { get; set; }
        public string Difficulty { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    // Enum for post types
    public enum PostType
    {
        Article,
        Question,
        Tutorial,
        News
    }

    // Main entities
    [Table("users")]
    public class User
    {
        [PartitionKey(0)]
        public Guid UserId { get; set; }
        
        [Column("username")]
        public string Username { get; set; } = string.Empty;
        
        [Column("email")]
        public string Email { get; set; } = string.Empty;
        
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;
        
        [Column("profile")]
        public UserProfile Profile { get; set; } = new();
        
        [Column("is_active")]
        public bool IsActive { get; set; }
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("followers_count")]
        public int FollowersCount { get; set; }
        
        [Column("following_count")]
        public int FollowingCount { get; set; }
    }

    [Table("posts")]
    public class Post
    {
        [PartitionKey(0)]
        public Guid PostId { get; set; }
        
        [ClusteringKey(0)]
        public Guid UserId { get; set; }
        
        [Column("title")]
        public string Title { get; set; } = string.Empty;
        
        [Column("content")]
        public string Content { get; set; } = string.Empty;
        
        [Column("post_type")]
        public PostType PostType { get; set; }
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        
        [Column("likes_count")]
        public int LikesCount { get; set; }
        
        [Column("comments_count")]
        public int CommentsCount { get; set; }
        
        [Column("shares_count")]
        public int SharesCount { get; set; }
        
        [Column("tags")]
        public List<string> Tags { get; set; } = new();
        
        [Column("metadata")]
        public PostMetadata Metadata { get; set; } = new();
    }

    [Table("comments")]
    public class Comment
    {
        [PartitionKey(0)]
        public Guid PostId { get; set; }
        
        [ClusteringKey(0)]
        public Guid CommentId { get; set; }
        
        [Column("user_id")]
        public Guid UserId { get; set; }
        
        [Column("content")]
        public string Content { get; set; } = string.Empty;
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [Column("likes_count")]
        public int LikesCount { get; set; }
        
        [Column("parent_comment_id")]
        public Guid? ParentCommentId { get; set; }
    }

    // Analytics and aggregation tables
    [Table("user_activity")]
    public class UserActivity
    {
        [PartitionKey(0)]
        public Guid UserId { get; set; }
        
        [ClusteringKey(0)]
        public DateTime ActivityDate { get; set; }
        
        [Column("posts_created")]
        public int PostsCreated { get; set; }
        
        [Column("comments_made")]
        public int CommentsMade { get; set; }
        
        [Column("likes_given")]
        public int LikesGiven { get; set; }
        
        [Column("time_spent_minutes")]
        public int TimeSpentMinutes { get; set; }
    }

    // Materialized views
    [MaterializedView("posts_by_popularity", "posts")]
    public class PostByPopularity
    {
        [PartitionKey(0)]
        public int LikesCount { get; set; }
        
        [ClusteringKey(0)]
        public DateTime CreatedAt { get; set; }
        
        [ClusteringKey(1)]
        public Guid PostId { get; set; }
        
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public PostType PostType { get; set; }
    }

    [MaterializedView("posts_by_user", "posts")]
    public class PostByUser
    {
        [PartitionKey(0)]
        public Guid UserId { get; set; }
        
        [ClusteringKey(0)]
        public DateTime CreatedAt { get; set; }
        
        [ClusteringKey(1)]
        public Guid PostId { get; set; }
        
        public string Title { get; set; } = string.Empty;
        public PostType PostType { get; set; }
        public int LikesCount { get; set; }
    }

    #endregion

    #region DbContext

    public class SocialMediaContext : CassandraDbContext
    {
        public CassandraDbSet<User> Users { get; set; } = null!;
        public CassandraDbSet<Post> Posts { get; set; } = null!;
        public CassandraDbSet<Comment> Comments { get; set; } = null!;
        public CassandraDbSet<UserActivity> UserActivities { get; set; } = null!;        public SocialMediaContext(CassandraConfiguration configuration, ILogger logger) 
            : base(configuration, logger)
        {
        }

        public new async Task CreateViewsAsync()
        {
            // Create materialized views - Note: Views are typically created through schema management
            // For this example, we'll just log that they would be created
            await Task.CompletedTask; // Placeholder for async signature
        }
    }

    #endregion

    #region User Management Example

    private static async Task UserManagementExample(SocialMediaContext context, ILogger logger)
    {
        logger.LogInformation("=== User Management Example ===");

        // Create sample users
        var users = new[]
        {
            new User
            {
                UserId = Guid.NewGuid(),
                Username = "alice_dev",
                Email = "alice@example.com",
                PasswordHash = "hashed_password_1",
                Profile = new UserProfile
                {
                    Bio = "Software developer passionate about distributed systems",
                    Website = "https://alice-dev.com",
                    Location = "San Francisco, CA",
                    JoinDate = DateTime.UtcNow.AddMonths(-6)
                },
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                FollowersCount = 1250,
                FollowingCount = 340
            },
            new User
            {
                UserId = Guid.NewGuid(),
                Username = "bob_writer",
                Email = "bob@example.com",
                PasswordHash = "hashed_password_2",
                Profile = new UserProfile
                {
                    Bio = "Technical writer and documentation enthusiast",
                    Website = "https://bobwrites.tech",
                    Location = "Austin, TX",
                    JoinDate = DateTime.UtcNow.AddMonths(-3)
                },
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-3),
                FollowersCount = 890,
                FollowingCount = 125
            },
            new User
            {
                UserId = Guid.NewGuid(),
                Username = "charlie_ops",
                Email = "charlie@example.com",
                PasswordHash = "hashed_password_3",
                Profile = new UserProfile
                {
                    Bio = "DevOps engineer specializing in cloud infrastructure",
                    Website = "",
                    Location = "Remote",
                    JoinDate = DateTime.UtcNow.AddDays(-45)
                },
                IsActive = false,
                CreatedAt = DateTime.UtcNow.AddDays(-45),
                FollowersCount = 456,
                FollowingCount = 78
            }
        };

        // Add users to the context
        context.Users.AddRange(users);
        
        // Save changes
        var saveResult = await context.SaveChangesAsync();
        logger.LogInformation("Saved {Count} users to database", saveResult);        // Query users using CassandraORM extension methods to avoid ambiguity
        var allUsers = await ((IQueryable<User>)context.Users).ToListAsync();
        logger.LogInformation("Retrieved {Count} users from database", allUsers.Count);

        // Query by specific criteria
        var activeUsers = await ((IQueryable<User>)context.Users).Where(u => u.IsActive)
            .OrderBy(u => u.Username).ToListAsync();
        
        logger.LogInformation("Found {Count} active users", activeUsers.Count);
    }

    #endregion

    #region Post and Comment Example

    private static async Task PostAndCommentExample(SocialMediaContext context, ILogger logger)
    {        logger.LogInformation("=== Post and Comment Example ===");
        
        // Get a user to create posts for
        var user = await ((IQueryable<User>)context.Users).FirstAsync();
        
        // Create posts
        var post1 = new Post
        {
            PostId = Guid.NewGuid(),
            UserId = user.UserId,
            Title = "Getting Started with Apache Cassandra",
            Content = "Apache Cassandra is a highly scalable NoSQL database...",
            PostType = PostType.Article,
            CreatedAt = DateTime.UtcNow,
            LikesCount = 25,
            CommentsCount = 5,
            SharesCount = 8,
            Tags = new List<string> { "cassandra", "nosql", "database", "distributed-systems" },
            // Using UDT for metadata
            Metadata = new PostMetadata
            {
                ReadTimeMinutes = 5,
                Difficulty = "Beginner",
                Category = "Technology"
            }
        };

        var post2 = new Post
        {
            PostId = Guid.NewGuid(),
            UserId = user.UserId,
            Title = "Advanced Query Patterns in Cassandra",
            Content = "Learn about efficient query patterns and data modeling...",
            PostType = PostType.Tutorial,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            LikesCount = 42,
            CommentsCount = 12,
            SharesCount = 15,
            Tags = new List<string> { "cassandra", "queries", "optimization", "data-modeling" },
            Metadata = new PostMetadata
            {
                ReadTimeMinutes = 12,
                Difficulty = "Advanced",
                Category = "Technology"
            }
        };

        context.Posts.AddRange(post1, post2);

        // Create comments
        var comment1 = new Comment
        {
            PostId = post1.PostId,
            CommentId = Guid.NewGuid(),
            UserId = user.UserId,
            Content = "Great article! Very helpful for beginners.",
            CreatedAt = DateTime.UtcNow.AddMinutes(-15),
            LikesCount = 3
        };

        var comment2 = new Comment
        {
            PostId = post1.PostId,
            CommentId = Guid.NewGuid(),
            UserId = user.UserId,
            Content = "Thanks for the feedback! Glad it was helpful.",
            CreatedAt = DateTime.UtcNow.AddMinutes(-25),
            LikesCount = 1,
            ParentCommentId = comment1.CommentId // This is a reply
        };

        context.Comments.Add(comment1);
        context.Comments.Add(comment2);

        var saveResult = await context.SaveChangesAsync();
        logger.LogInformation("Saved {Count} posts and comments", saveResult);

        // Query posts by user
        var userPosts = await System.Linq.Queryable.Where(context.Posts, p => p.UserId == user.UserId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        logger.LogInformation("User has {Count} posts", userPosts.Count);

        // Query comments for a post
        var postComments = await System.Linq.Queryable.Where(context.Comments, c => c.PostId == post1.PostId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        logger.LogInformation("Post has {Count} comments", postComments.Count);
    }

    #endregion

    #region Materialized View Example

    private static async Task MaterializedViewExample(SocialMediaContext context, ILogger logger)
    {
        logger.LogInformation("=== Materialized View Example ===");

        // Query posts by popularity (using materialized view)
        var popularPosts = await System.Linq.Queryable.Where(context.View<PostByPopularity>(), p => p.LikesCount >= 20)
            .OrderByDescending(p => p.LikesCount)
            .ToListAsync();

        logger.LogInformation("Found {Count} popular posts", popularPosts.Count);        // Query posts by a specific user (using materialized view)
        var user = await ((IQueryable<User>)context.Users).FirstAsync();
        var userPostsView = await ((IQueryable<PostByUser>)context.View<PostByUser>())
            .Where(p => p.UserId == user.UserId)
            .OrderByDescending(p => p.CreatedAt).ToListAsync();

        logger.LogInformation("Found {Count} posts by user in materialized view", userPostsView.Count);

        // Query recent posts across all users
        var recentPosts = await System.Linq.Queryable.Where(context.View<PostByUser>(), p => p.CreatedAt >= DateTime.UtcNow.AddDays(-7))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        logger.LogInformation("Found {Count} posts in the last week", recentPosts.Count);

        // Query posts by type
        var tutorialPosts = await System.Linq.Queryable.Where(context.View<PostByUser>(), p => p.PostType == PostType.Tutorial)
            .OrderByDescending(p => p.LikesCount)
            .ToListAsync();

        logger.LogInformation("Found {Count} tutorial posts", tutorialPosts.Count);
    }

    #endregion

    #region Analytics Example

    private static async Task AnalyticsExample(SocialMediaContext context, ILogger logger)
    {
        logger.LogInformation("=== Analytics Example ===");        // Create sample analytics data
        var users = await ((IQueryable<User>)context.Users).ToListAsync();
        
        foreach (var user in users)
        {
            for (int i = 0; i < 7; i++)
            {
                var activity = new UserActivity
                {
                    UserId = user.UserId,
                    ActivityDate = DateTime.UtcNow.Date.AddDays(-i),
                    PostsCreated = Random.Shared.Next(0, 3),
                    CommentsMade = Random.Shared.Next(0, 10),
                    LikesGiven = Random.Shared.Next(0, 20),
                    TimeSpentMinutes = Random.Shared.Next(5, 120)
                };
                
                context.UserActivities.Add(activity);
            }
        }

        var savedActivities = await context.SaveChangesAsync();
        logger.LogInformation("Saved {Count} user activity records", savedActivities);

        // Query analytics data
        var user1 = users.First();
        var userAnalytics = await System.Linq.Queryable.Where(context.UserActivities, a => a.UserId == user1.UserId)
            .OrderByDescending(a => a.ActivityDate)
            .ToListAsync();

        logger.LogInformation("Found {Count} activity records for user", userAnalytics.Count);

        // Calculate aggregated metrics
        var userStats = new
        {
            TotalPosts = userAnalytics.Sum(a => a.PostsCreated),
            TotalComments = userAnalytics.Sum(a => a.CommentsMade),
            TotalLikes = userAnalytics.Sum(a => a.LikesGiven),
            TotalTimeSpent = userAnalytics.Sum(a => a.TimeSpentMinutes),
            AverageTimePerDay = userAnalytics.Average(a => a.TimeSpentMinutes)
        };

        logger.LogInformation("User stats - Posts: {Posts}, Comments: {Comments}, Likes: {Likes}, Total Time: {Time}min", 
            userStats.TotalPosts, userStats.TotalComments, userStats.TotalLikes, userStats.TotalTimeSpent);
    }

    #endregion

    #region Advanced Query Example

    private static async Task AdvancedQueryExample(SocialMediaContext context, ILogger logger)
    {
        logger.LogInformation("=== Advanced Query Example ===");        // Complex aggregation query
        var analyticsData = await ((IQueryable<UserActivity>)context.UserActivities).ToListAsync();
        
        var dailyStats = analyticsData
            .GroupBy(a => a.ActivityDate.Date)
            .Select(g => new
            {
                Date = g.Key,
                TotalUsers = g.Select(a => a.UserId).Distinct().Count(),
                TotalPosts = g.Sum(a => a.PostsCreated),
                TotalComments = g.Sum(a => a.CommentsMade),
                AverageTimeSpent = g.Average(a => a.TimeSpentMinutes)
            })
            .OrderByDescending(s => s.Date)
            .ToList();

        logger.LogInformation("Generated daily statistics for {Count} days", dailyStats.Count);

        // Query with multiple conditions
        var activePosts = await System.Linq.Queryable.Where(context.Posts, p => p.LikesCount >= 10)
            .ToListAsync();

        logger.LogInformation("Found {Count} posts with significant engagement", activePosts.Count);

        // Time-based queries
        var recentActivity = await System.Linq.Queryable.Where(context.UserActivities, a => a.ActivityDate >= DateTime.UtcNow.AddDays(-3))
            .OrderByDescending(a => a.ActivityDate)
            .ToListAsync();

        logger.LogInformation("Found {Count} recent activity records", recentActivity.Count);

        // Demonstration of raw CQL usage for complex queries
        var rawQueryResults = await context.Posts
            .FromCqlAsync("SELECT * FROM posts WHERE post_type = ? ALLOW FILTERING", PostType.Article);

        logger.LogInformation("Raw CQL query returned {Count} articles", rawQueryResults.Count());

        // Using pagination with Take
        var first5Posts = await System.Linq.Queryable.Take(context.Posts, 5).ToListAsync();

        logger.LogInformation("Retrieved first {Count} posts using pagination", first5Posts.Count);

        // Complex filtering with multiple criteria
        var complexQuery = await System.Linq.Queryable.Where(context.Posts, p => p.LikesCount >= 10)
            .Where(p => p.CreatedAt >= DateTime.UtcNow.AddDays(-30))
            .ToListAsync();

        logger.LogInformation("Complex query returned {Count} recent popular posts", complexQuery.Count);
    }

    #endregion
}

#region Entity Models

[Table("users")]
public class User
{
    [PartitionKey]
    public Guid UserId { get; set; }

    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("profile")]
    public UserProfile Profile { get; set; } = new();

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("followers_count")]
    public int FollowersCount { get; set; }

    [Column("following_count")]
    public int FollowingCount { get; set; }
}

[Table("posts")]
public class Post
{
    [PartitionKey]
    [Column("post_id")]
    public Guid PostId { get; set; }

    [ClusteringKey(0)]
    [Column("user_id")]
    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    [Column("post_type")]
    public PostType PostType { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("likes_count")]
    public int LikesCount { get; set; }

    [Column("comments_count")]
    public int CommentsCount { get; set; }

    [Column("shares_count")]
    public int SharesCount { get; set; }

    // Collections
    [Collection(CollectionType.List)]
    public List<string> Tags { get; set; } = new();

    // User-Defined Type
    public PostMetadata Metadata { get; set; } = new();
}

[Table("comments")]
public class Comment
{
    [PartitionKey]
    [Column("post_id")]
    public Guid PostId { get; set; }

    [ClusteringKey(0)]
    [Column("comment_id")]
    public Guid CommentId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    public string Content { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("likes_count")]
    public int LikesCount { get; set; }

    [Column("parent_comment_id")]
    public Guid? ParentCommentId { get; set; }
}

[Table("user_activity")]
public class UserActivity
{
    [PartitionKey]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ClusteringKey(0)]
    [Column("activity_date")]
    public DateTime ActivityDate { get; set; }

    [Column("posts_created")]
    public int PostsCreated { get; set; }

    [Column("comments_made")]
    public int CommentsMade { get; set; }

    [Column("likes_given")]
    public int LikesGiven { get; set; }

    [Column("time_spent_minutes")]
    public int TimeSpentMinutes { get; set; }
}

public enum PostType
{
    Article,
    Question,
    Tutorial,
    News
}

#endregion

#region User-Defined Types

[UserDefinedType("user_profile")]
public class UserProfile
{
    public string Bio { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; }
}

[UserDefinedType("post_metadata")]
public class PostMetadata
{
    [Column("read_time_minutes")]
    public int ReadTimeMinutes { get; set; }

    public string Difficulty { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

#endregion

#region Materialized Views

[MaterializedView("posts_by_popularity", "posts")]
public class PostByPopularity
{
    [PartitionKey(0)]
    public int LikesCount { get; set; }

    [ClusteringKey(0)]
    public DateTime CreatedAt { get; set; }

    [ClusteringKey(1)]
    public Guid PostId { get; set; }

    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public PostType PostType { get; set; }
}

[MaterializedView("posts_by_user", "posts")]
public class PostByUser
{
    [PartitionKey(0)]
    public Guid UserId { get; set; }

    [ClusteringKey(0)]
    public DateTime CreatedAt { get; set; }

    [ClusteringKey(1)]
    public Guid PostId { get; set; }

    public string Title { get; set; } = string.Empty;
    public PostType PostType { get; set; }
    public int LikesCount { get; set; }
}

#endregion

#region DbContext

public class SocialMediaContext : CassandraDbContext
{
    public CassandraDbSet<User> Users { get; set; } = null!;
    public CassandraDbSet<Post> Posts { get; set; } = null!;
    public CassandraDbSet<Comment> Comments { get; set; } = null!;
    public CassandraDbSet<UserActivity> UserActivities { get; set; } = null!;

    public SocialMediaContext(CassandraConfiguration configuration, ILogger logger) 
        : base(configuration, logger)
    {
        // Register User-Defined Types
        RegisterUdt<UserProfile>();
        RegisterUdt<PostMetadata>();

        // Register Materialized Views
        RegisterView<PostByPopularity, Post>();
        RegisterView<PostByUser, Post>();        }

        public new async Task CreateViewsAsync()
        {
            // Create materialized views - Note: Views are typically created through schema management
            // For this example, we'll just log that they would be created
            await Task.CompletedTask; // Placeholder for async signature
        }
    }

    #endregion
