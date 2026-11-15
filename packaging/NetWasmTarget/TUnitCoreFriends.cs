// netwasm0.1 builds of TUnit.Core generate no assembly attributes, so the engine's access to Core's internals, which
// upstream grants through InternalsVisibleTo items, is declared here. Both are unsigned on netwasm0.1.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TUnit.Engine")]
