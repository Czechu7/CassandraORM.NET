using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cassandra;
using CassandraOrm.Core;
using CassandraOrm.Configuration;
using CassandraOrm.Mapping;
using CassandraOrm.UDT;
using CassandraOrm.MaterializedViews;
using CassandraOrm.Collections;
using Microsoft.Extensions.Logging;

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
            await CollectionOperationsExample(context, logger);
            await AdvancedQueryExample(context, logger);

            logger.LogInformation("All examples completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during execution");
        }
    }

    #region User Management Example
    
    private static async Task UserManagementExample(SocialMediaContext context, ILogger logger)
    {
        logger.LogInformation("=== User Management Example ===");

        // Create users with UDT data
        var user1 = new User
        {
            UserId = Guid.NewGuid(),
            Username = "john_doe",
            Email = "john@example.com",
            FullName = "John Doe",
            Bio = "Software developer passionate about distributed systems",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            FollowersCount = 150,
            FollowingCount = 89,
            // Using UDT for contact information
            ContactInfo = new ContactInfo
            {
                PhoneNumber = "+1-555-0123",
                Country = "USA",
                City = "San Francisco",
                PostalCode = "94105"
            },
            // Using collections
            Interests = new HashSet<string> { "programming", "technology", "music" },
            SocialLinks = new Dictionary<string, string>
            {
                ["twitter"] = "@johndoe",
                ["linkedin"] = "linkedin.com/in/johndoe",
                ["github"] = "github.com/johndoe"
            }
        };

        var user2 = new User
        {
            UserId = Guid.NewGuid(),
            Username = "jane_smith",
            Email = "jane@example.com",
            FullName = "Jane Smith",
            Bio = "Product manager and tech enthusiast",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            FollowersCount = 320,
            FollowingCount = 145,
            ContactInfo = new ContactInfo
            {
                PhoneNumber = "+1-555-0456",
                Country = "USA",
                City = "New York",
                PostalCode = "10001"
            },
            Interests = new HashSet<string> { "product-management", "startups", "design" },
            SocialLinks = new Dictionary<string, string>
            {
                ["twitter"] = "@janesmith",
                ["linkedin"] = "linkedin.com/in/janesmith"
            }
        };

        // Add users to context
        context.Users.Add(user1);
        context.Users.Add(user2);

        // Save changes
        var saveResult = await context.SaveChangesAsync();
        logger.LogInformation("Saved {Count} users to database", saveResult);

        // Query users
        var allUsers = await context.Users.ToListAsync();
        logger.LogInformation("Retrieved {Count} users from database", allUsers.Count);

        // Query by specific criteria
        var activeUsers = await context.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Username)
            .ToListAsync();
        
        logger.LogInformation("Found {Count} active users", activeUsers.Count);
    }

    #endregion

    #region Post and Comment Example

    private static async Task PostAndCommentExample(SocialMediaContext context, ILogger logger)
    {
        logger.LogInformation("=== Post and Comment Example ===");

        // Get a user to create posts for
        var user = await context.Users.FirstAsync();
        
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
            Title = "Building Microservices with .NET",
            Content = "Microservices architecture has become increasingly popular...",
            PostType = PostType.Tutorial,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            LikesCount = 42,
            CommentsCount = 12,
            SharesCount = 15,
            Tags = new List<string> { "dotnet", "microservices", "architecture", "csharp" },
            Metadata = new PostMetadata
            {
                ReadTimeMinutes = 8,
                Difficulty = "Intermediate",
                Category = "Software Development"
            }
        };

        context.Posts.Add(post1);
        context.Posts.Add(post2);

        // Create comments
        var comment1 = new Comment
        {
            CommentId = Guid.NewGuid(),
            PostId = post1.PostId,
            UserId = user.UserId,
            Content = "Great introduction to Cassandra! Very helpful for beginners.",
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            LikesCount = 3,
            // Nested comment (reply)
            ParentCommentId = null
        };

        var comment2 = new Comment
        {
            CommentId = Guid.NewGuid(),
            PostId = post1.PostId,
            UserId = user.UserId,
            Content = "Thanks for the feedback! I'm glad you found it useful.",
            CreatedAt = DateTime.UtcNow.AddMinutes(-25),
            LikesCount = 1,
            ParentCommentId = comment1.CommentId // This is a reply
        };

        context.Comments.Add(comment1);
        context.Comments.Add(comment2);

        var saveResult = await context.SaveChangesAsync();
        logger.LogInformation("Saved {Count} posts and comments", saveResult);

        // Query posts by user
        var userPosts = await context.Posts
            .Where(p => p.UserId == user.UserId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        logger.LogInformation("User has {Count} posts", userPosts.Count);

        // Query comments for a post
        var postComments = await context.Comments
            .Where(c => c.PostId == post1.PostId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        logger.LogInformation("Post has {Count} comments", postComments.Count);
    }

    #endregion

    #region Materialized View Example

    private static async Task MaterializedViewExample(SocialMediaContext context, ILogger logger)
    {
        logger.LogInformation("=== Materialized View Example ===");

        // Query popular posts (using materialized view)
        var popularPosts = await context.PopularPosts
            .Where(p => p.LikesCount >= 20)
            .OrderByDescending(p => p.LikesCount)
            .ToListAsync();

        logger.LogInformation("Found {Count} popular posts", popularPosts.Count);

        // Query posts by category (using materialized view)
        var techPosts = await context.PostsByCategory
            .Where(p => p.Category == "Technology")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        logger.LogInformation("Found {Count} technology posts", techPosts.Count);

        // Query user activity (using materialized view)
        var recentActivity = await context.UserActivity
            .Where(a => a.ActivityDate >= DateTime.UtcNow.Date)
            .OrderByDescending(a => a.ActivityDate)
            .ToListAsync();

        logger.LogInformation("Found {Count} recent activities", recentActivity.Count);
    }

    #endregion

    #region Collection Operations Example

    private static async Task CollectionOperationsExample(SocialMediaContext context, ILogger logger)
    {
        logger.LogInformation("=== Collection Operations Example ===");

        var user = await context.Users.FirstAsync();

        // Use collection update builder for efficient collection operations
        var collectionUpdates = user.UpdateCollections()
            .AddToSet(nameof(User.Interests), "machine-learning")
            .AddToSet(nameof(User.Interests), "artificial-intelligence")
            .PutInMap(nameof(User.SocialLinks), "youtube", "youtube.com/johndoe")
            .PutInMap(nameof(User.SocialLinks), "instagram", "@johndoe_dev");

        // Apply collection updates
        var updateCount = await context.SaveCollectionChangesAsync(user);
        logger.LogInformation("Applied {Count} collection updates", updateCount);

        // Verify updates
        var updatedUser = await context.Users
            .Where(u => u.UserId == user.UserId)
            .FirstAsync();

        logger.LogInformation("User now has {Count} interests and {Count} social links",
            updatedUser.Interests.Count, updatedUser.SocialLinks.Count);
    }

    #endregion

    #region Advanced Query Example

    private static async Task AdvancedQueryExample(SocialMediaContext context, ILogger logger)
    {
        logger.LogInformation("=== Advanced Query Example ===");

        // Complex query with multiple conditions
        var recentPopularPosts = await context.Posts
            .Where(p => p.CreatedAt >= DateTime.UtcNow.AddDays(-7))
            .Where(p => p.LikesCount >= 10)
            .OrderByDescending(p => p.LikesCount)
            .ThenByDescending(p => p.CreatedAt)
            .Take(10)
            .ToListAsync();

        logger.LogInformation("Found {Count} recent popular posts", recentPopularPosts.Count);

        // Raw CQL query for advanced scenarios
        var topUsers = await context.ExecuteCqlAsync(@"
            SELECT user_id, username, followers_count 
            FROM users 
            WHERE followers_count >= ? 
            ALLOW FILTERING", 100);

        logger.LogInformation("Found {Count} users with 100+ followers", topUsers.Count());

        // Batch operations
        var analyticsData = new List<UserAnalytics>();
        var users = await context.Users.Take(5).ToListAsync();

        foreach (var user in users)
        {
            analyticsData.Add(new UserAnalytics
            {
                UserId = user.UserId,
                AnalyticsDate = DateTime.UtcNow.Date,
                PostsCount = await context.Posts.Where(p => p.UserId == user.UserId).CountAsync(),
                TotalLikes = await context.Posts
                    .Where(p => p.UserId == user.UserId)
                    .SumAsync(p => p.LikesCount),
                EngagementRate = 0.0 // Would be calculated based on complex logic
            });
        }

        // Add analytics data in batch
        foreach (var analytics in analyticsData)
        {
            context.UserAnalytics.Add(analytics);
        }

        var analyticsCount = await context.SaveChangesAsync();
        logger.LogInformation("Saved {Count} analytics records", analyticsCount);
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

    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    public string Bio { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("followers_count")]
    public int FollowersCount { get; set; }

    [Column("following_count")]
    public int FollowingCount { get; set; }

    // User-Defined Type
    [Column("contact_info")]
    public ContactInfo ContactInfo { get; set; } = new();

    // Collections
    [Collection(CollectionType.Set)]
    public HashSet<string> Interests { get; set; } = new();

    [Collection(CollectionType.Map)]
    [Column("social_links")]
    public Dictionary<string, string> SocialLinks { get; set; } = new();
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

    [Column("likes_count")]
    [Counter]
    public long LikesCount { get; set; }

    [Column("comments_count")]
    [Counter]
    public long CommentsCount { get; set; }

    [Column("shares_count")]
    [Counter]
    public long SharesCount { get; set; }

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
    [Column("comment_id")]
    public Guid CommentId { get; set; }

    [ClusteringKey(0)]
    [Column("post_id")]
    public Guid PostId { get; set; }

    [ClusteringKey(1)]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    public string Content { get; set; } = string.Empty;

    [Column("likes_count")]
    [Counter]
    public long LikesCount { get; set; }

    [Column("parent_comment_id")]
    public Guid? ParentCommentId { get; set; }
}

[Table("user_analytics")]
public class UserAnalytics
{
    [PartitionKey]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ClusteringKey(0, descending: true)]
    [Column("analytics_date")]
    public DateTime AnalyticsDate { get; set; }

    [Column("posts_count")]
    public int PostsCount { get; set; }

    [Column("total_likes")]
    public long TotalLikes { get; set; }

    [Column("engagement_rate")]
    public double EngagementRate { get; set; }
}

public enum PostType
{
    Article,
    Tutorial,
    News,
    Discussion,
    Question
}

#endregion

#region User-Defined Types

[UserDefinedType("contact_info")]
public class ContactInfo
{
    [Column("phone_number")]
    public string PhoneNumber { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    [Column("postal_code")]
    public string PostalCode { get; set; } = string.Empty;
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

[MaterializedView("popular_posts", "posts")]
[MaterializedViewWhere("likes_count >= 10")]
public class PopularPost
{
    [PartitionKey]
    [Column("likes_count")]
    public long LikesCount { get; set; }

    [ClusteringKey(0)]
    [Column("post_id")]
    public Guid PostId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("post_type")]
    public PostType PostType { get; set; }
}

[MaterializedView("posts_by_category", "posts")]
[MaterializedViewWhere("metadata.category IS NOT NULL")]
public class PostByCategory
{
    [PartitionKey]
    [Column("category")]
    public string Category { get; set; } = string.Empty;

    [ClusteringKey(0, descending: true)]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ClusteringKey(1)]
    [Column("post_id")]
    public Guid PostId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    [Column("likes_count")]
    public long LikesCount { get; set; }
}

[MaterializedView("user_activity", "user_analytics")]
[MaterializedViewWhere("analytics_date IS NOT NULL")]
public class UserActivityView
{
    [PartitionKey]
    [Column("analytics_date")]
    public DateTime ActivityDate { get; set; }

    [ClusteringKey(0)]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("posts_count")]
    public int PostsCount { get; set; }

    [Column("total_likes")]
    public long TotalLikes { get; set; }

    [Column("engagement_rate")]
    public double EngagementRate { get; set; }
}

#endregion

#region DbContext

public class SocialMediaContext : CassandraDbContext
{
    public CassandraDbSet<User> Users { get; set; } = null!;
    public CassandraDbSet<Post> Posts { get; set; } = null!;
    public CassandraDbSet<Comment> Comments { get; set; } = null!;
    public CassandraDbSet<UserAnalytics> UserAnalytics { get; set; } = null!;

    // Materialized Views
    public CassandraDbSet<PopularPost> PopularPosts { get; set; } = null!;
    public CassandraDbSet<PostByCategory> PostsByCategory { get; set; } = null!;
    public CassandraDbSet<UserActivityView> UserActivity { get; set; } = null!;

    public SocialMediaContext(CassandraConfiguration configuration, ILogger? logger = null)
        : base(configuration, logger)
    {
        // Register User-Defined Types
        RegisterUdt<ContactInfo>();
        RegisterUdt<PostMetadata>();

        // Register Materialized Views
        RegisterView<PopularPost, Post>();
        RegisterView<PostByCategory, Post>();
        RegisterView<UserActivityView, UserAnalytics>();
    }
}

#endregion
