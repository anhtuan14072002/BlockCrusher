// Core.Game and its runtime bootstrap are owned by the com.tuna.core package.
// A second project-local copy would make both initializers create a scene named
// "Core", which throws as soon as the package cache is restored on a clean machine.
