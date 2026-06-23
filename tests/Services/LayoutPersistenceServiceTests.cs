using System;
using System.IO;
using Aero.Docking;
using Aero.Services;
using Dock.Model.Controls;
using Dock.Serializer;
using Xunit;

namespace Aero.Tests.Services;

/// <summary>
/// Tests for LayoutPersistenceService - tests the underlying DockSerializer round-trip.
/// </summary>
public class LayoutPersistenceServiceTests
{
    [Fact]
    public void LayoutPersistenceService_CanBeCreated()
    {
        // Arrange & Act
        var service = new LayoutPersistenceService();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Serialize_And_Deserialize_RoundTrip()
    {
        // Arrange
        var layout = AeroDockFactory.CreateDefaultLayout();
        var serializer = new DockSerializer();

        // Act
        var json = serializer.Serialize(layout);
        var loaded = serializer.Deserialize<IRootDock>(json);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("Root", loaded.Id);
    }

    [Fact]
    public void Deserialize_Throws_WhenJsonIsCorrupted()
    {
        // Arrange
        var serializer = new DockSerializer();
        var corruptedJson = "not valid json {{{";

        // Act & Assert - Newtonsoft throws on deserialization failure
        Assert.Throws<Newtonsoft.Json.JsonReaderException>(
            () => serializer.Deserialize<IRootDock>(corruptedJson));
    }
}