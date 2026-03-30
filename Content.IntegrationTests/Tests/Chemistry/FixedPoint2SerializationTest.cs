using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.IntegrationTests.Tests.Chemistry
{
    /// <summary>
    /// Tests for FixedPoint2 YAML serialization.
    /// Note: FixedPoint2 core functionality is tested in Content.Tests/Shared/Chemistry/FixedPoint2_Tests.cs
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public sealed class FixedPoint2SerializationTest
    {
        [Test]
        public async Task DeserializeNullTest()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var serialization = server.ResolveDependency<Robust.Shared.Serialization.Manager.ISerializationManager>();

            var node = ValueDataNode.Null();
            var unit = serialization.Read<FixedPoint2?>(node);

            Assert.That(unit, Is.Null);

            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task SerializeNullTest()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var serialization = server.ResolveDependency<Robust.Shared.Serialization.Manager.ISerializationManager>();

            var node = serialization.WriteValue<FixedPoint2?>(null);
            Assert.That(node.IsNull);

            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task SerializeNullableValueTest()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var serialization = server.ResolveDependency<Robust.Shared.Serialization.Manager.ISerializationManager>();

            var node = serialization.WriteValue<FixedPoint2?>(FixedPoint2.New(2.5f));
#pragma warning disable NUnit2045 // Interdependent assertions
            Assert.That(node is ValueDataNode);
            Assert.That(((ValueDataNode)node).Value, Is.EqualTo("2.5"));
#pragma warning restore NUnit2045

            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task DeserializeNullDefinitionTest()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var serialization = server.ResolveDependency<Robust.Shared.Serialization.Manager.ISerializationManager>();

            var node = new MappingDataNode().Add("unit", ValueDataNode.Null());
            var definition = serialization.Read<FixedPoint2TestDefinition>(node);

            Assert.That(definition.Unit, Is.Null);

            await pair.CleanReturnAsync();
        }
    }

    [DataDefinition]
    public sealed partial class FixedPoint2TestDefinition
    {
        [DataField] public FixedPoint2? Unit { get; set; } = FixedPoint2.New(5);
    }
}
