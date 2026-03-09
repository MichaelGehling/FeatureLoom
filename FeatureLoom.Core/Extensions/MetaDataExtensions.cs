using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using System;

namespace FeatureLoom.Extensions
{
    public static class MetaDataExtensions
    {
        /// <summary>
        /// Attaches metadata to the given object using the specified key.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <typeparam name="D">The type of the metadata value.</typeparam>
        /// <param name="obj">The object to attach metadata to.</param>
        /// <param name="key">The key for the metadata entry.</param>
        /// <param name="data">The metadata value to store.</param>
        public static void SetMetaData<T, D>(this T obj, string key, D data) where T : class => MetaData.SetMetaData(obj, key, data);

        /// <summary>
        /// Tries to retrieve metadata of the specified key and type from the given object.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <typeparam name="D">The expected type of the metadata value.</typeparam>
        /// <param name="obj">The object to retrieve metadata from.</param>
        /// <param name="key">The key for the metadata entry.</param>
        /// <param name="data">The retrieved metadata value, if found and of the correct type.</param>
        /// <returns>True if the metadata was found and of the correct type; otherwise, false.</returns>
        public static bool TryGetMetaData<T, D>(this T obj, string key, out D data) where T : class => MetaData.TryGetMetaData(obj, key, out data);

        /// <summary>
        /// Gets a unique handle for the given object, which can be used for tracking or referencing the object.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="obj">The object to get a handle for.</param>
        /// <returns>An <see cref="ObjectHandle"/> representing the object.</returns>
        public static ObjectHandle GetHandle<T>(this T obj) where T : class => MetaData.GetHandle(obj);

        /// <summary>
        /// Gets a <see cref="FeatureLock"/> associated with the given object.
        /// This lock can be used to synchronize access to the object.
        /// This requires a lookup which is less performant than using a FeatureLock directly.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="obj">The object to get a lock for.</param>
        /// <returns>A <see cref="FeatureLock"/> instance associated with the object.</returns>
        public static FeatureLock GetLock<T>(this T obj) where T : class => FeatureLock.GetLockFor(obj);

        /// <summary>
        /// Attaches a custom destructor action to the object, which will be invoked when the object is finalized (garbage collected).
        /// This is useful for custom cleanup logic, such as disposing resources.
        /// Important: It is not guaranteed when the object will be garbage collected and hence when the destructor code will be called.
        /// Adiitionally, this adds a significant cost for the garbage collection process.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="obj">The object to attach the destructor to.</param>
        /// <param name="destructor">The action to invoke during finalization.</param>
        public static void AttachDetructor<T>(this T obj, Action<T> destructor) where T : class
        {
            MetaData.GetOrCreate(obj).MetaDataUpdateSender.ProcessMessage<MetaData.DestructionInfo>(_ => destructor(obj));
        }
    }
}