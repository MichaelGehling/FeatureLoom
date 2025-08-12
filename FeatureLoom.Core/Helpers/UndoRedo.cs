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
    UndoRedoStack undos;
    UndoRedoStack redos;

    // Used to notify listeners about undo/redo state changes.
    Sender<Notification> updateSender = new Sender<Notification>();
    Forwarder<Notification> updateForwarder = new Forwarder<Notification>();

    // Tracks the number of active transactions.
    volatile int transactionCounter = 0;
    private readonly int historyLimit;

    /// <summary>
    /// Initializes a new instance of the UndoRedo class.
    /// </summary>
    /// <param name="historyLimit">The maximum number of undo actions to keep. A value <= 0 means no limit. The default is 0.</param>
    public UndoRedo(int historyLimit = 0)
    {
        this.historyLimit = historyLimit;
        this.undos = new UndoRedoStack(historyLimit);
        this.redos = new UndoRedoStack(historyLimit);
        // Only forward notifications when not inside a transaction.
        updateSender.ConnectTo(new DeactivatableForwarder(() => this.transactionCounter == 0)).ConnectTo(updateForwarder);
    }

    /// <summary>
    /// Gets the maximum number of undo actions that will be stored.
    /// A value of 0 or less means the history is unlimited.
    /// </summary>
    public int HistoryLimit => historyLimit;

    /// <summary>
    /// Types of notifications sent when undo/redo state changes.
    /// </summary>
    public enum Notification
    {
        /// <summary>
        /// An undo operation was successfully performed.
        /// </summary>
        UndoPerformed,
        /// <summary>
        /// A redo operation was successfully performed.
        /// </summary>
        RedoPerformed,
        /// <summary>
        /// A new undo action was added to the stack.
        /// </summary>
        UndoJobAdded,
        /// <summary>
        /// A new redo action was added to the stack (typically during an undo operation).
        /// </summary>
        RedoJobAdded,
        /// <summary>
        /// The undo and redo stacks have been cleared.
        /// </summary>
        Cleared
    }

    /// <summary>
    /// Represents a single undo or redo action, which can be synchronous or asynchronous.
    /// </summary>
    private readonly struct UndoRedoAction
    {
        readonly object action;
        readonly string description;

        /// <summary>
        /// Initializes a new instance of the <see cref="UndoRedoAction"/> struct for an asynchronous action.
        /// </summary>
        /// <param name="asyncAction">The asynchronous action to execute.</param>
        /// <param name="description">A description of the action.</param>
        public UndoRedoAction(Func<Task> asyncAction, string description) : this()
        {
            this.action = asyncAction;
            this.description = description;                
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UndoRedoAction"/> struct for a synchronous action.
        /// </summary>
        /// <param name="action">The synchronous action to execute.</param>
        /// <param name="description">A description of the action.</param>
        public UndoRedoAction(Action action, string description) : this()
        {
            this.action = action;
            this.description = description;                
        }

        /// <summary>
        /// Executes the action synchronously. If the action is async, it will be awaited blocking the current thread.
        /// </summary>
        public void Execute()
        {
            if (action is Action syncAction) syncAction();
            else if (action is Func<Task> asyncAction) asyncAction().WaitFor();
        }

        /// <summary>
        /// Executes the action asynchronously. If the action is sync, it will be executed synchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
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

    private class UndoRedoStack
    {
        private readonly LinkedList<UndoRedoAction> actions = new LinkedList<UndoRedoAction>();
        private readonly int historyLimit;

        public UndoRedoStack(int historyLimit)
        {
            this.historyLimit = historyLimit;
        }

        public int Count => actions.Count;

        public IEnumerable<string> Descriptions => actions.Reverse().Select(action => action.Description);

        public void Push(UndoRedoAction action)
        {
            actions.AddLast(action);
            if (historyLimit > 0 && actions.Count > historyLimit)
            {
                RemoveOldest();
            }
        }

        public UndoRedoAction Pop()
        {
            if (actions.Count == 0) throw new InvalidOperationException("The stack is empty.");
            var action = actions.Last.Value;
            actions.RemoveLast();
            return action;
        }

        public void Clear() => actions.Clear();

        private void RemoveOldest() => actions.RemoveFirst();
    }

    /// <summary>
    /// Represents a transaction that groups multiple undo actions into a single combined action.
    /// When the transaction is disposed, all undo actions registered during its lifetime are merged.
    /// This is useful for complex operations that consist of multiple smaller steps, which should be undone as a single unit.
    /// </summary>
    /// <example>
    /// <code>
    /// using (undoRedo.StartTransaction("Complex Edit"))
    /// {
    ///     // Multiple operations with individual undo actions
    ///     undoRedo.DoWithUndo(() => data.Add(1), () => data.Remove(1));
    ///     undoRedo.DoWithUndo(() => data.Add(2), () => data.Remove(2));
    /// } // All undos are now combined into one "Complex Edit" undo action.
    /// </code>
    /// </example>
    public struct Transaction : IDisposable
    {
        UndoRedo serviceInstance;
        int numUndosAtStart;
        FeatureLock.LockHandle lockHandle;
        string description;

        /// <summary>
        /// Begins a new transaction, acquiring the lock and incrementing the transaction counter.
        /// This constructor is intended for internal use. Use <see cref="UndoRedo.StartTransaction"/> to create a transaction.
        /// </summary>
        /// <param name="serviceInstance">The <see cref="UndoRedo"/> instance.</param>
        /// <param name="lockHandle">The lock handle to ensure thread safety.</param>
        /// <param name="description">The description for the combined undo action.</param>
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
        /// It then releases the lock.
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
    /// Gets the number of available undo actions.
    /// </summary>
    public int NumUndos => undos.Count;

    /// <summary>
    /// Gets the number of available redo actions.
    /// </summary>
    public int NumRedos => redos.Count;

    /// <summary>
    /// Gets a value indicating whether an undo operation is currently in progress.
    /// This is useful to prevent re-entrant calls or to update UI elements.
    /// </summary>
    public bool CurrentlyUndoing => undoing;

    /// <summary>
    /// Gets a value indicating whether a redo operation is currently in progress.
    /// This is useful to prevent re-entrant calls or to update UI elements.
    /// </summary>
    public bool CurrentlyRedoing => redoing;

    /// <summary>
    /// Starts a new transaction for grouping undo actions. Use with a `using` statement to ensure it's properly disposed.
    /// </summary>
    /// <param name="description">An optional description for the combined undo action. If null, a description will be generated from the combined actions.</param>
    /// <returns>A <see cref="Transaction"/> object that should be disposed to end the transaction.</returns>
    public Transaction StartTransaction(string description = null) => new Transaction(this, myLock.LockReentrant(), description);

    /// <summary>
    /// Starts a new asynchronous transaction for grouping undo actions. Use with a `using` statement to ensure it's properly disposed.
    /// </summary>
    /// <param name="description">An optional description for the combined undo action. If null, a description will be generated from the combined actions.</param>
    /// <returns>A task that resolves to a <see cref="Transaction"/> object that should be disposed to end the transaction.</returns>
    public async Task<Transaction> StartTransactionAsync(string description = null) => new Transaction(this, await myLock.LockReentrantAsync().ConfiguredAwait(), description);

    /// <summary>
    /// Gets the source for receiving undo/redo state change notifications.
    /// Connect a message sink to this source to be notified of events like <see cref="Notification.UndoPerformed"/> or <see cref="Notification.UndoJobAdded"/>.
    /// </summary>
    public IMessageSource<Notification> UpdateNotificationSource => updateForwarder;

    /// <summary>
    /// Gets the descriptions of all available undo actions, from most recent to oldest.
    /// </summary>
    public IEnumerable<string> UndoDescriptions => undos.Descriptions;

    /// <summary>
    /// Gets the descriptions of all available redo actions, from most recent to oldest.
    /// </summary>
    public IEnumerable<string> RedoDescriptions => redos.Descriptions;

    /// <summary>
    /// Performs the most recent undo action, if available.
    /// The corresponding redo action is automatically added to the redo stack.
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
    /// The corresponding redo action is automatically added to the redo stack.
    /// </summary>
    /// <returns>A task that completes when the undo operation is finished.</returns>
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
    /// The corresponding undo action is automatically added back to the undo stack.
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
    /// The corresponding undo action is automatically added back to the undo stack.
    /// </summary>
    /// <returns>A task that completes when the redo operation is finished.</returns>
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
    /// Adds a synchronous undo action to the stack. When a new action is added, the redo stack is cleared unless a redo is in progress.
    /// </summary>
    /// <param name="undo">The synchronous action that will reverse a change.</param>
    /// <param name="description">An optional description of what this action undoes.</param>
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
    /// Adds an asynchronous undo action to the stack. When a new action is added, the redo stack is cleared unless a redo is in progress.
    /// </summary>
    /// <param name="undo">The asynchronous action that will reverse a change.</param>
    /// <param name="description">An optional description of what this action undoes.</param>
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
    /// Executes a synchronous action and registers its corresponding undo action.
    /// This method simplifies the common pattern of performing an operation and immediately adding its inverse to the undo stack.
    /// When the undo action is performed, it will call this method again with the actions swapped, enabling redo functionality.
    /// </summary>
    /// <param name="doAction">The synchronous action to execute.</param>
    /// <param name="undoAction">The synchronous action that will undo the `doAction`.</param>
    /// <param name="description">An optional description for the undo action.</param>
    public void DoWithUndo(Action doAction, Action undoAction, string description = null)
    {
        doAction.Invoke();

        AddUndo(() =>
        {
            DoWithUndo(undoAction, doAction, description);
        }, description);
    }

    /// <summary>
    /// Asynchronously executes an action and registers its corresponding undo action.
    /// This method simplifies the common pattern of performing an async operation and immediately adding its inverse to the undo stack.
    /// When the undo action is performed, it will call this method again with the actions swapped, enabling redo functionality.
    /// </summary>
    /// <param name="doAction">The asynchronous action to execute.</param>
    /// <param name="undoAction">The asynchronous action that will undo the `doAction`.</param>
    /// <param name="description">An optional description for the undo action.</param>
    /// <returns>A task that completes when the `doAction` has finished.</returns>
    public async Task DoWithUndoAsync(Func<Task> doAction, Func<Task> undoAction, string description = null)
    {
        await doAction().ConfiguredAwait();

        AddUndo(async () =>
        {
            await DoWithUndoAsync(undoAction, doAction, description).ConfiguredAwait();
        }, description);
    }

    /// <summary>
    /// Clears all undo and redo actions from their respective stacks. This cannot be undone.
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
    /// Combines the last N undo actions on the current stack into a single composite action.
    /// If an undo operation is in progress (<see cref="CurrentlyUndoing"/> is true), it combines actions from the redo stack.
    /// Otherwise, it combines actions from the undo stack.
    /// This is useful for grouping a series of operations into a single undo/redo step, for example in a transaction.
    /// </summary>
    /// <param name="numUndosToCombine">The number of actions to combine from the top of the stack. Must be 2 or more.</param>
    /// <param name="description">An optional description for the new combined action. If null, a description is generated by joining the descriptions of the combined actions.</param>
    /// <returns><c>true</c> if the actions were successfully combined; otherwise, <c>false</c> (e.g., if there are not enough actions to combine).</returns>
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