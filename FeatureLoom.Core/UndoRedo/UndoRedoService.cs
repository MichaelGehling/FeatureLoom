using FeatureLoom.MessageFlow;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;

namespace FeatureLoom.UndoRedo
{
    public class UndoRedoService
    {
        FeatureLock myLock = new FeatureLock();

        bool undoing = false;
        bool redoing = false;
        Stack<UndoRedoAction> undos = new Stack<UndoRedoAction>();
        Stack<UndoRedoAction> redos = new Stack<UndoRedoAction>();
        Sender<Notification> updateSender = new Sender<Notification>();
        Forwarder<Notification> updateForwarder = new Forwarder<Notification>();
        int transactionCounter = 0;

        public UndoRedoService()
        {
            updateSender.ConnectTo(new DeactivatableForwarder(() => this.transactionCounter == 0)).ConnectTo(updateForwarder);
        }

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
            UndoRedoService serviceInstance;
            int numUndosAtStart;
            FeatureLock.LockHandle lockHandle;
            string description;

            public Transaction(UndoRedoService serviceInstance, FeatureLock.LockHandle lockHandle, string description)
            {
                this.serviceInstance = serviceInstance;
                serviceInstance.transactionCounter++;
                this.lockHandle = lockHandle;
                numUndosAtStart = this.serviceInstance.NumUndos;
                this.description = description;
            }

            public void Dispose()
            {
                serviceInstance.transactionCounter--;
                int numUndosInTransaction = this.serviceInstance.NumUndos - numUndosAtStart;
                this.serviceInstance.TryCombineLastUndos(numUndosInTransaction, description);                
                lockHandle.Dispose();
            }
        }

        public int NumUndos => undos.Count;
        public int NumRedos => redos.Count;
        public bool CurrentlyUndoing => undoing;
        public bool CurrentlyRedoing => redoing;

        public Transaction StartTransaction(string description = null) => new Transaction(this, myLock.LockReentrant(), description);
        public async Task<Transaction> StartTransactionAsync(string description = null) => new Transaction(this, await myLock.LockReentrantAsync().ConfigureAwait(false), description);

        public IMessageSource<Notification> UpdateNotificationSource => updateForwarder;

        public IEnumerable<string> UndoDescriptions => undos.Select(action => action.Description);
        public IEnumerable<string> RedoDescriptions => redos.Select(action => action.Description);

        public void PerformUndo()
        {
            if (undos.Count == 0) return;

            using (myLock.LockReentrant())
            {
                undoing = true;
                try
                {
                    undos.Pop().Execute();
                }
                finally
                {
                    undoing = false;
                }
            }

            updateSender.Send(Notification.UndoPerformed);
            Log.INFO(this.GetHandle(), "Undo permformed");
        }

        public void PerformRedo()
        {
            if (redos.Count == 0) return;

            using (myLock.LockReentrant())
            {
                redoing = true;
                try
                {
                    redos.Pop().Execute();
                }
                finally
                {
                    redoing = false;
                }
            }

            updateSender.Send(Notification.RedoPerformed);
            Log.INFO(this.GetHandle(), "Redo permformed");
        }

        public void AddUndo(Action undo, string description = null)
        {
            using (myLock.LockReentrant())
            {
                if (undoing)
                {
                    redos.Push(new UndoRedoAction(undo, description));
                }
                else
                {
                    if (!redoing) redos.Clear();
                    undos.Push(new UndoRedoAction(undo, description));
                }
            }
            if (undoing)
            {
                updateSender.Send(Notification.RedoJobAdded);
                Log.INFO(this.GetHandle(), "Redo job added");
            }
            else
            {
                updateSender.Send(Notification.UndoJobAdded);
                Log.INFO(this.GetHandle(), "Undo job added");
            }
        }

        public void DoWithUndo(Action doAction, Action undoAction, string description = null)
        {
            doAction.Invoke();

            AddUndo(() =>
            {
                undoAction.Invoke();
                DoWithUndo(undoAction, doAction, description);
            }, description);
        }

        /*
        public async Task DoWithUndoAsync(Func<Task> doAction, Func<Task> undoAction)
        {
            await doAction.Invoke().ConfigureAwait(false);

            AddUndo(() =>
            {
                undoAction.Invoke();
                DoWithUndo(undoAction, doAction);
            });
        }
        */

        public void Clear()
        {
            using (myLock.LockReentrant())
            {
                undos.Clear();
                redos.Clear();
            }

            updateSender.Send(Notification.Cleared);
            Log.INFO(this.GetHandle(), "All undo and redo jobs cleared");
        }


        public bool TryCombineLastUndos(int numUndosToCombine = 2, string description = null)
        {
            using (myLock.LockReentrant())
            {
                if (CurrentlyUndoing) 
                {
                    if (numUndosToCombine > NumRedos) return false;
                    if (numUndosToCombine < 2) return false;

                    UndoRedoAction[] combinedActions = new UndoRedoAction[numUndosToCombine];
                    for (int i = 0; i < numUndosToCombine; i++)
                    {
                        combinedActions[i] = redos.Pop();
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
                    }, description ?? combinedActions.Select(action => action.Description).AllItemsToString<string, IEnumerable<string>>("; \n"));
                }
                else
                {
                    if (numUndosToCombine > NumUndos) return false;
                    if (numUndosToCombine < 2) return false;

                    UndoRedoAction[] combinedActions = new UndoRedoAction[numUndosToCombine];
                    for (int i = 0; i < numUndosToCombine; i++)
                    {
                        combinedActions[i] = undos.Pop();
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
                    }, description ?? combinedActions.Select(action => action.Description).AllItemsToString<string, IEnumerable<string>>("; \n"));
                }

                return true;
            }
        }
    }
}