using System.IO;

namespace FeatureLoom.TCP
{
    public interface IStreamUpgrader
    {
        Stream Upgrade(Stream stream);
    }
}