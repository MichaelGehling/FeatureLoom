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

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides undo/redo functionality with support for transactions, async actions, and thread safety.
///
/// <para><b>Usability:</b></para>
/// <para>
/// This class is designed for maximum ease of use. To register an undoable operation, simply provide an action to perform and an action that does the opposite.
/// This makes it very straightforward to add undo/redo support to any in-memory workflow, without the need to wrap changes in serializable command objects or manage complex state.
/// </para>
///
/// <para><b>Limitation - No Persistence:</b></para>
/// <para>
/// The main limitation is that actions are stored as delegates (lambdas or method references), which cannot be serialized or persisted.
/// As a result, the undo/redo history is lost when the application exits, and cannot be transferred between sessions or processes.
/// </para>
///
/// <para><b>Design Trade-off:</b></para>
/// <para>
/// This design favors simplicity and developer productivity for session-based or in-memory scenarios.
/// Supporting persistent undo/redo would require a more complex architecture, such as serializable command objects, which is more error-prone and harder to maintain.
/// </para>
///
/// <para><b>Summary:</b></para>
/// <para>
/// For most applications, this class provides a pragmatic and developer-friendly solution for undo/redo functionality.
/// If persistent or cross-session undo/redo is required, a different approach should be considered.
/// </para>
/// </summary>
public class UndoRedo
{
    // Lock to ensure thread safety for all operations.
    FeatureLock myLock = new FeatureLock();

    // Indicates if an undo or redo operation is currently in progress.
    bool undoing = false;
    bool redoing = false;

    // Stacks to hold undo and redo actions.
    Stack<UndoRedoAction> undos = new Stack<UndoRedoAction>();
    Stack<UndoRedoAction> redos = new Stack<UndoRedoAction>();

    // Used to notify listeners about undo/redo state changes.
    Sender<Notification> updateSender = new Sender<Notification>();
    Forwarder<Notification> updateForwarder = new Forwarder<Notification>();

    // Tracks the number of active transactions.
    volatile int transactionCounter = 0;

    /// <summary>
    /// Initializes a new instance of the UndoRedo class.
    /// </summary>
    public UndoRedo()
    {
        // Only forward notifications when not inside a transaction.
        updateSender.ConnectTo(new DeactivatableForwarder(() => this.transactionCounter == 0)).ConnectTo(updateForwarder);
    }

    /// <summary>
    /// Types of notifications sent when undo/redo state changes.
    /// </summary>
    public enum Notification
    {
        UndoPerformed,
        RedoPerformed,
        UndoJobAdded,
        RedoJobAdded,
        Cleared
    }

    /// <summary>
    /// Represents a single undo or redo action, which can be synchronous or asynchronous.
    /// </summary>
    private readonly struct UndoRedoAction
    {
        readonly object action;
        readonly string description;

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

        /// <summary>
        /// Executes the action synchronously.
        /// </summary>
        public void Execute()
        {
            if (action is Action syncAction) syncAction();
            else if (action is Func<Task> asyncAction) asyncAction().WaitFor();
        }

        /// <summary>
        /// Executes the action asynchronously.
        /// </summary>
        public Task ExecuteAsync()
        {
            if (action is Action syncAction) syncAction();
            else if (action is Func<Task> asyncAction) return asyncAction();
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Description of the action.
        /// </summary>
        public string Description => description;
    }

    /// <summary>
    /// Represents a transaction that groups multiple undo actions into a single combined action.
    /// </summary>
    public struct Transaction : IDisposable
    {
        UndoRedo serviceInstance;
        int numUndosAtStart;
        FeatureLock.LockHandle lockHandle;
        string description;

        /// <summary>
        /// Begins a new transaction, acquiring the lock and incrementing the transaction counter.
        /// </summary>
        public Transaction(UndoRedo serviceInstance, FeatureLock.LockHandle lockHandle, string description)
        {
            this.serviceInstance = serviceInstance;
            serviceInstance.transactionCounter++;
            this.lockHandle = lockHandle;
            numUndosAtStart = this.serviceInstance.NumUndos;
            this.description = description;
        }

        /// <summary>
        /// Ends the transaction, combining all undo actions performed during the transaction into one.
        /// </summary>
        public void Dispose()
        {
            serviceInstance.transactionCounter--;
            int numUndosInTransaction = this.serviceInstance.NumUndos - numUndosAtStart;
            this.serviceInstance.TryCombineLastUndos(numUndosInTransaction, description);
            lockHandle.Dispose();
        }
    }

    /// <summary>
    /// Number of available undo actions.
    /// </summary>
    public int NumUndos => undos.Count;

    /// <summary>
    /// Number of available redo actions.
    /// </summary>
    public int NumRedos => redos.Count;

    /// <summary>
    /// True if an undo operation is currently in progress.
    /// </summary>
    public bool CurrentlyUndoing => undoing;

    /// <summary>
    /// True if a redo operation is currently in progress.
    /// </summary>
    public bool CurrentlyRedoing => redoing;

    /// <summary>
    /// Starts a new transaction for grouping undo actions.
    /// </summary>
    public Transaction StartTransaction(string description = null) => new Transaction(this, myLock.LockReentrant(), description);

    /// <summary>
    /// Starts a new transaction asynchronously for grouping undo actions.
    /// </summary>
    public async Task<Transaction> StartTransactionAsync(string description = null) => new Transaction(this, await myLock.LockReentrantAsync().ConfiguredAwait(), description);

    /// <summary>
    /// Source for receiving undo/redo state change notifications.
    /// </summary>
    public IMessageSource<Notification> UpdateNotificationSource => updateForwarder;

    /// <summary>
    /// Descriptions of all available undo actions.
    /// </summary>
    public IEnumerable<string> UndoDescriptions => undos.Select(action => action.Description);

    /// <summary>
    /// Descriptions of all available redo actions.
    /// </summary>
    public IEnumerable<string> RedoDescriptions => redos.Select(action => action.Description);

    /// <summary>
    /// Performs the most recent undo action, if available.
    /// </summary>
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
        OptLog.INFO()?.Build("Undo performed");
    }

    /// <summary>
    /// Asynchronously performs the most recent undo action, if available.
    /// </summary>
    public async Task PerformUndoAsync()
    {
        if (undos.Count == 0) return;

        using (await myLock.LockReentrantAsync().ConfiguredAwait())
        {
            undoing = true;
            try
            {
                await undos.Pop().ExecuteAsync().ConfiguredAwait();
            }
            finally
            {
                undoing = false;
            }
        }

        await updateSender.SendAsync(Notification.UndoPerformed).ConfiguredAwait();
        OptLog.INFO()?.Build("Undo performed");
    }

    /// <summary>
    /// Performs the most recent redo action, if available.
    /// </summary>
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
        OptLog.INFO()?.Build("Redo performed");
    }

    /// <summary>
    /// Asynchronously performs the most recent redo action, if available.
    /// </summary>
    public async Task PerformRedoAsync()
    {
        if (redos.Count == 0) return;

        using (await myLock.LockReentrantAsync().ConfiguredAwait())
        {
            redoing = true;
            try
            {
                await redos.Pop().ExecuteAsync().ConfiguredAwait();
            }
            finally
            {
                redoing = false;
            }
        }

        await updateSender.SendAsync(Notification.RedoPerformed).ConfiguredAwait();
        OptLog.INFO()?.Build("Redo performed");
    }

    /// <summary>
    /// Adds a synchronous undo action to the stack.
    /// </summary>
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
            OptLog.INFO()?.Build("Redo job added");
        }
        else
        {
            updateSender.Send(Notification.UndoJobAdded);
            OptLog.INFO()?.Build("Undo job added");
        }
    }

    /// <summary>
    /// Adds an asynchronous undo action to the stack.
    /// </summary>
    public void AddUndo(Func<Task> undo, string description = null)
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
            OptLog.INFO()?.Build("Redo job added");
        }
        else
        {
            updateSender.Send(Notification.UndoJobAdded);
            OptLog.INFO()?.Build("Undo job added");
        }
    }

    /// <summary>
    /// Executes an action and registers its undo action for reversal.
    /// </summary>
    public void DoWithUndo(Action doAction, Action undoAction, string description = null)
    {
        doAction.Invoke();

        AddUndo(() =>
        {
            DoWithUndo(undoAction, doAction, description);
        }, description);
    }

    /// <summary>
    /// Asynchronously executes an action and registers its undo action for reversal.
    /// </summary>
    public async Task DoWithUndoAsync(Func<Task> doAction, Func<Task> undoAction, string description = null)
    {
        await doAction().ConfiguredAwait();

        AddUndo(async () =>
        {
            await DoWithUndoAsync(undoAction, doAction, description).ConfiguredAwait();
        }, description);
    }

    /// <summary>
    /// Clears all undo and redo actions.
    /// </summary>
    public void Clear()
    {
        using (myLock.LockReentrant())
        {
            undos.Clear();
            redos.Clear();
        }

        updateSender.Send(Notification.Cleared);
        OptLog.INFO()?.Build("All undo and redo jobs cleared");
    }

    /// <summary>
    /// Combines the last N undo or redo actions into a single action.
    /// </summary>
    /// <param name="numUndosToCombine">Number of actions to combine.</param>
    /// <param name="description">Optional description for the combined action.</param>
    /// <returns>True if the actions were combined; otherwise, false.</returns>
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