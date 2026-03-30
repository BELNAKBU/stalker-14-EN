using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization.Manager;
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
        private ISerializationManager _serialization = null!;
        private IReflectionManager _reflection = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Initialize IoC manually to avoid dependency on RobustUnitTest base class
            // This ensures the test works consistently in both VS Test Explorer and dotnet test CLI

            // First, initialize the IoC context on this thread
            IoCManager.InitThread();

            // Then build the graph and register dependencies
            IoCManager.BuildGraph();
            IoCManager.Register<IReflectionManager, ReflectionManager>();
            IoCManager.Register<ISerializationManager, SerializationManager>();

            _reflection = IoCManager.Resolve<IReflectionManager>();
            _serialization = IoCManager.Resolve<ISerializationManager>();

            // Load assemblies containing FixedPoint2 and test types
            _reflection.LoadAssemblies(new[] {
                typeof(FixedPoint2).Assembly,
                typeof(FixedPoint2SerializationTest).Assembly
            });

            _serialization.Initialize();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            IoCManager.Clear();
        }

        [Test]
        public void DeserializeNullTest()
        {
            var node = ValueDataNode.Null();
            var unit = _serialization.Read<FixedPoint2?>(node);

            Assert.That(unit, Is.Null);
        }

        [Test]
        public void SerializeNullTest()
        {
            var node = _serialization.WriteValue<FixedPoint2?>(null);
            Assert.That(node.IsNull);
        }

        [Test]
        public void SerializeNullableValueTest()
        {
            var node = _serialization.WriteValue<FixedPoint2?>(FixedPoint2.New(2.5f));
#pragma warning disable NUnit2045 // Interdependent assertions
            Assert.That(node is ValueDataNode);
            Assert.That(((ValueDataNode)node).Value, Is.EqualTo("2.5"));
#pragma warning restore NUnit2045
        }

        [Test]
        public void DeserializeNullDefinitionTest()
        {
            var node = new MappingDataNode().Add("unit", ValueDataNode.Null());
            var definition = _serialization.Read<FixedPoint2TestDefinition>(node);

            Assert.That(definition.Unit, Is.Null);
        }
    }

    [DataDefinition]
    public sealed partial class FixedPoint2TestDefinition
    {
        [DataField] public FixedPoint2? Unit { get; set; } = FixedPoint2.New(5);
    }
}
