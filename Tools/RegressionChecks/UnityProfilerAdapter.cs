// The managed runner exercises persistence logic, not Unity profiler timings.
namespace Unity.Profiling
{
    public readonly struct ProfilerMarker
    {
        public ProfilerMarker(string name) { }
        public AutoScope Auto() => default;
        public readonly struct AutoScope : System.IDisposable
        {
            public void Dispose() { }
        }
    }
}
