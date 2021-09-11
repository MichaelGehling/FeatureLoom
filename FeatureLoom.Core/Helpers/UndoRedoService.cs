using FeatureLoom.MessageFlow;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using FeatureLoom.Extensions;

namespace FeatureLoom.Helpers
{
    public static class UndoRedoService
    {
        private class ContextData : IServiceContextData
        {
            public FeatureLock myLock = new FeatureLock();

            public bool undoing = false;
            public bool redoing = false;
            public Stack<UndoRedoAction> undos = new Stack<UndoRedoAction>();
            public Stack<UndoRedoAction> redos = new Stack<UndoRedoAction>();
            public Sender<Notification> updateSender = new Sender<Notification>();

            public IServiceContextData Copy()
            {
                using (myLock.LockReadOnly())
                {
                    var newContext = new ContextData();
                    newContext.undoing = undoing;
                    newContext.redoing = redoing;
                    newContext.undos = new Stack<UndoRedoAction>(undos);
                    newContext.redos = new Stack<UndoRedoAction>(redos);
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

        private struct UndoRedoAction
        {
            object action;            
            string description;

            public UndoRedoAction(Func<Task> asyncAction, string description) : this()
            {
                this.action = asyncAction;
                this.description = description;                
            }

            public UndoRedoAction(Action action, string description) : this()
            {
                this.action = action;
                this.description = description;                
            }

            public void Execute()
            {
                if (action is Action syncAction) syncAction();
                else if (action is Func<Task> asyncAction) asyncAction().WaitFor();
            }

            public Task ExecuteAsync()
            {
                if (action is Action syncAction) syncAction();
                else if (action is Func<Task> asyncAction) return asyncAction();
                
                return Task.CompletedTask;
            }

            public string Description => description;
        }

        public struct Transaction : IDisposable
        {
            int numUndosAtStart;
            FeatureLock.LockHandle lockHandle;
            string description;

            public Transaction(FeatureLock.LockHandle lockHandle, string description)
            {
                this.lockHandle = lockHandle;
                numUndosAtStart = UndoRedoService.NumUndos;
                this.description = description;
            }

            public void Dispose()
            {
                int numUndosInTransaction = UndoRedoService.NumUndos - numUndosAtStart;
                UndoRedoService.TryCombineLastUndos(numUndosInTransaction, description);
                lockHandle.Dispose();
            }
        }

        public static int NumUndos => context.Data.undos.Count;
        public static int NumRedos => context.Data.redos.Count;
        public static bool CurrentlyUndoing => context.Data.undoing;
        public static bool CurrentlyRedoing => context.Data.redoing;

        public static Transaction StartTransaction(string description = null) => new Transaction(context.Data.myLock.LockReentrant(), description);
        public static async Task<Transaction> StartTransactionAsync(string description = null) => new Transaction(await context.Data.myLock.LockReentrantAsync(), description);

        public static IMessageSource<Notification> UpdateNotificationSource => context.Data.updateSender;

        public static void PerformUndo()
        {
            if (context.Data.undos.Count == 0) return;

            using (context.Data.myLock.LockReentrant())
            {
                context.Data.undoing = true;
                try
                {
                    context.Data.undos.Pop().Execute();
                }
                finally
                {
                    context.Data.undoing = false;
                }
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
                try
                {
                    context.Data.redos.Pop().Execute();
                }
                finally
                {
                    context.Data.redoing = false;
                }
            }

            context.Data.updateSender.Send(Notification.RedoPerformed);
            Log.INFO(context.Data.GetHandle(), "Redo permformed");
        }

        public static void AddUndo(Action undo, string description = null)
        {
            using (context.Data.myLock.LockReentrant())
            {
                if (context.Data.undoing)
                {
                    context.Data.redos.Push(new UndoRedoAction(undo, description));
                }
                else
                {
                    if (!context.Data.redoing) context.Data.redos.Clear();
                    context.Data.undos.Push(new UndoRedoAction(undo, description));
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

        public static void DoWithUndo(Action doAction, Action undoAction, string description = null)
        {
            doAction.Invoke();

            AddUndo(() =>
            {
                undoAction.Invoke();
                DoWithUndo(undoAction, doAction, description);
            }, description);
        }

        /*
        public static async Task DoWithUndoAsync(Func<Task> doAction, Func<Task> undoAction)
        {
            await doAction.Invoke();

            AddUndo(() =>
            {
                undoAction.Invoke();
                DoWithUndo(undoAction, doAction);
            });
        }
        */

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

        public static bool TryCombineLastUndos(int numUndosToCombine = 2, string description = null)
        {
            var data = context.Data;
            using (data.myLock.LockReentrant())
            {
                if (CurrentlyUndoing) 
                {
                    if (numUndosToCombine > NumRedos) return false;
                    if (numUndosToCombine < 2) return false;

                    UndoRedoAction[] combinedActions = new UndoRedoAction[numUndosToCombine];
                    for (int i = 0; i < numUndosToCombine; i++)
                    {
                        combinedActions[i] = data.redos.Pop();
                    }

                    AddUndo(() =>
                    {
                        int numUndosBefore = NumUndos;
                        foreach (UndoRedoAction action in combinedActions)
                        {
                            action.Execute();
                        }

                        int numNewUndos = NumUndos - numUndosBefore;
                        if (numNewUndos >= 2)
                        {
                            TryCombineLastUndos(numNewUndos);
                        }
                    }, description ?? combinedActions.Select(action => action.Description).AllItemsToString("; \n"));
                }
                else
                {
                    if (numUndosToCombine > NumUndos) return false;
                    if (numUndosToCombine < 2) return false;

                    UndoRedoAction[] combinedActions = new UndoRedoAction[numUndosToCombine];
                    for (int i = 0; i < numUndosToCombine; i++)
                    {
                        combinedActions[i] = data.undos.Pop();
                    }

                    AddUndo(() =>
                    {
                        int numRedosBefore = NumRedos;
                        foreach (UndoRedoAction action in combinedActions)
                        {
                            action.Execute();
                        }

                        int numNewRedos = NumRedos - numRedosBefore;
                        if (numNewRedos >= 2)
                        {
                            TryCombineLastUndos(numNewRedos);
                        }
                    }, description ?? combinedActions.Select(action => action.Description).AllItemsToString("; \n"));
                }

                return true;
            }
        }
    }
}