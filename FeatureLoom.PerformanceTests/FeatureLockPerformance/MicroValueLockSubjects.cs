﻿using FeatureLoom.Synchronization;
using System;
using System.Runtime.CompilerServices;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance
{
    public class MicroValueLockSubjects
    {
        private MicroValueLock myLock = new MicroValueLock();

        public void Init() => myLock = new MicroValueLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            myLock.Enter();
            try
            {
                action();
            }
            finally
            {
                myLock.Exit();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            myLock.Enter();
            try
            {
            }
            finally
            {
                myLock.Exit();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly()
        {
            myLock.EnterReadOnly();
            try
            {
            }
            finally
            {
                myLock.ExitReadOnly();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {
            if (myLock.TryEnter())
            {
                try
                {
                }
                finally
                {
                    myLock.Exit();
                }
            }
        }
    }
}