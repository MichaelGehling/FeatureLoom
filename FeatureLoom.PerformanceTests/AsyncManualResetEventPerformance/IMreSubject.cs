using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance
{
    public interface IMreSubject
    {
        void Set1();
        void Reset1();
        void Wait1();
        Task WaitAsync1();
        Task Job1 { get; set; }

        void Set2();
        void Reset2();
        void Wait2();
        Task WaitAsync2();
        Task Job2 { get; set; }
    }
}
