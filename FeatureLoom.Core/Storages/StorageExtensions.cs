using FeatureLoom.MessageFlow;
using System.Threading.Tasks;

namespace FeatureLoom.Storages
{
    public static class StorageExtensions
    {
        public static bool TrySubscribeForChangeUpdate<T>(this IStorageReader reader, string uriPattern, IMessageSink updateSink)
        {
            var converter = new MessageConverter<ChangeNotification, ChangeUpdate<T>>(note => new ChangeUpdate<T>(note, reader));
            if (reader.TrySubscribeForChangeNotifications(uriPattern, converter))
            {
                converter.ConnectTo(updateSink);
                return true;
            }
            return false;
        }

        public static async Task<bool> TryCopy(this IStorageReader reader, string sourceUri, IStorageWriter writer, string targetUri)
        {
            bool success = true;
            success &= await reader.TryReadAsync(sourceUri, async stream =>
            {
                success &= await writer.TryWriteAsync(targetUri, stream);                
            });
            return success;
        }

        public static Task<bool> TryCopy(this StorageService storage, string sourceCategory, string sourceUri, string targetCategory, string targetUri)
        {
            var reader = storage.GetReader(sourceCategory);
            var writer = storage.GetWriter(targetCategory);
            return reader.TryCopy(sourceUri, writer, targetUri);
        }
    }
}