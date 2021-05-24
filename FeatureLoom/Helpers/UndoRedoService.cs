using FeatureLoom.MessageFlow;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Helpers
{
    public static class UndoRedoService
    {
        private class ContextData : IServiceContextData
        {
            public FeatureLock myLock = new FeatureLock();

            public bool undoing = false;
            public bool redoing = false;
            public Stack<Action> undos = new Stack<Action>();
            public Stack<Action> redos = new Stack<Action>();
            public Sender<Notification> updateSender = new Sender<Notification>();

            public IServiceContextData Copy()
            {
                using (myLock.LockReadOnly())
                {
                    var newContext = new ContextData();
                    newContext.undoing = undoing;
                    newContext.redoing = redoing;
                    newContext.undos = new Stack<Action>(undos);
                    newContext.redos = new Stack<Action>(redos);
                    foreach (var sink in newContext.updateSender.GetConnectedSinks())
                    {
                        updateSender.ConnectTo(sink);
                    }

                    return newContext;
                }
            }
        }

        private static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

        public enum Notification
        {
            UndoPerformed,
            RedoPerformed,
            UndoJobAdded,
            RedoJobAdded,
            Cleared
        }

        public static int NumUndos => context.Data.undos.Count;
        public static int NumRedos => context.Data.redos.Count;
        public static bool CurrentlyUndoing => context.Data.undoing;
        public static bool CurrentlyRedoing => context.Data.redoing;

        public static IMessageSource<Notification> UpdateNotificationSource => context.Data.updateSender;

        public static void PerformUndo()
        {
            if (context.Data.undos.Count == 0) return;

            using (context.Data.myLock.LockReentrant())
            {
                context.Data.undoing = true;
                context.Data.undos.Pop().Invoke();
                context.Data.undoing = false;
            }

            context.Data.updateSender.Send(Notification.UndoPerformed);
            Log.INFO(context.Data.GetHandle(), "Undo permformed");
        }

        public static void PerformRedo()
        {
            if (context.Data.redos.Count == 0) return;

            using (context.Data.myLock.LockReentrant())
            {
                context.Data.redoing = true;
                context.Data.redos.Pop().Invoke();
                context.Data.redoing = false;
            }

            context.Data.updateSender.Send(Notification.RedoPerformed);
            Log.INFO(context.Data.GetHandle(), "Redo permformed");
        }

        public static void AddUndo(Action undo)
        {
            using (context.Data.myLock.LockReentrant())
            {
                if (context.Data.undoing)
                {
                    context.Data.redos.Push(undo);
                }
                else
                {
                    if (!context.Data.redoing) context.Data.redos.Clear();
                    context.Data.undos.Push(undo);
                }
            }
            if (context.Data.undoing)
            {
                context.Data.updateSender.Send(Notification.RedoJobAdded);
                Log.INFO(context.Data.GetHandle(), "Redo job added");
            }
            else
            {
                context.Data.updateSender.Send(Notification.UndoJobAdded);
                Log.INFO(context.Data.GetHandle(), "Undo job added");
            }
        }

        public static void Clear()
        {
            using (context.Data.myLock.LockReentrant())
            {
                context.Data.undos.Clear();
                context.Data.redos.Clear();
            }

            context.Data.updateSender.Send(Notification.Cleared);
            Log.INFO(context.Data.GetHandle(), "All undo and redo jobs cleared");
        }
    }
}