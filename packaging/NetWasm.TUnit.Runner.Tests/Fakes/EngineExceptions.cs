// The engine wraps a test's exception in its reporting exceptions; the host classifies the one beneath.
namespace TUnit.Engine.Exceptions;

internal sealed class TestFailedException(Exception inner) : Exception("[Test Failure] " + inner.Message, inner);
