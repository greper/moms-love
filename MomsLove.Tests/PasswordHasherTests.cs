using MomsLove.Core;

namespace MomsLove.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Create_RejectsEmptyPassword()
    {
        Assert.Throws<ArgumentException>(() => PasswordHasher.Create(""));
    }

    [Fact]
    public void Verify_AcceptsCorrectPasswordOnly()
    {
        var settings = PasswordHasher.Create("parent-secret");

        Assert.True(PasswordHasher.Verify(settings, "parent-secret"));
        Assert.False(PasswordHasher.Verify(settings, "wrong"));
    }
}
