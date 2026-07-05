using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VideoCortex.Core.Entities;

namespace VideoCortex.Tests;

public class DbContextTests
{
    [Fact]
    public void Project_And_Video_RoundTrip_And_Relationship()
    {
        using var fx = new SqliteInMemoryFixture();

        using (var ctx = fx.CreateContext())
        {
            var project = new Project { Name = "Local LLMs", Slug = "local-llms", CreatedAt = DateTime.UtcNow };
            project.Videos.Add(new Video { YoutubeVideoId = "abc12345678", Status = VideoStatus.Added, AddedAt = DateTime.UtcNow });
            ctx.Projects.Add(project);
            ctx.SaveChanges();
        }

        using (var ctx = fx.CreateContext())
        {
            var project = ctx.Projects.Include(p => p.Videos).Single();
            project.Slug.Should().Be("local-llms");
            project.Videos.Should().ContainSingle()
                .Which.YoutubeVideoId.Should().Be("abc12345678");
        }
    }

    [Fact]
    public void Deleting_Project_Cascades_To_Videos()
    {
        using var fx = new SqliteInMemoryFixture();

        using (var ctx = fx.CreateContext())
        {
            var project = new Project { Name = "P", Slug = "p", CreatedAt = DateTime.UtcNow };
            project.Videos.Add(new Video { YoutubeVideoId = "vid00000001", AddedAt = DateTime.UtcNow });
            project.Videos.Add(new Video { YoutubeVideoId = "vid00000002", AddedAt = DateTime.UtcNow });
            ctx.Projects.Add(project);
            ctx.SaveChanges();
        }

        using (var ctx = fx.CreateContext())
        {
            var project = ctx.Projects.Include(p => p.Videos).Single();
            ctx.Projects.Remove(project);
            ctx.SaveChanges();
        }

        using (var ctx = fx.CreateContext())
        {
            ctx.Projects.Should().BeEmpty();
            ctx.Videos.Should().BeEmpty();
        }
    }

    [Fact]
    public void Duplicate_Project_Slug_Is_Rejected()
    {
        using var fx = new SqliteInMemoryFixture();
        using var ctx = fx.CreateContext();

        ctx.Projects.Add(new Project { Name = "One", Slug = "dup", CreatedAt = DateTime.UtcNow });
        ctx.Projects.Add(new Project { Name = "Two", Slug = "dup", CreatedAt = DateTime.UtcNow });

        var act = () => ctx.SaveChanges();
        act.Should().Throw<DbUpdateException>();
    }

    [Fact]
    public void Duplicate_Video_Within_Project_Is_Rejected()
    {
        using var fx = new SqliteInMemoryFixture();

        int projectId;
        using (var ctx = fx.CreateContext())
        {
            var project = new Project { Name = "P", Slug = "p", CreatedAt = DateTime.UtcNow };
            ctx.Projects.Add(project);
            ctx.SaveChanges();
            projectId = project.Id;
        }

        using (var ctx = fx.CreateContext())
        {
            ctx.Videos.Add(new Video { ProjectId = projectId, YoutubeVideoId = "same0000001", AddedAt = DateTime.UtcNow });
            ctx.Videos.Add(new Video { ProjectId = projectId, YoutubeVideoId = "same0000001", AddedAt = DateTime.UtcNow });

            var act = () => ctx.SaveChanges();
            act.Should().Throw<DbUpdateException>();
        }
    }
}
