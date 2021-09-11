using FeatureLoom.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace FeatureLoom.Helpers
{
    
    public class UndoRedoTests
    {
        string data = "Init";
        void ChangeDataAndAddUndo(string newValue)
        {
            string origValue = data;
            data = newValue;
            UndoRedoService.AddUndo(() =>
            {
                ChangeDataAndAddUndo(origValue);
            });
        }

        [Fact]
        public void CanUndoAndRedoMultipleSteps()
        {
            TestHelper.PrepareTestContext();

            Assert.Equal("Init", data);
            ChangeDataAndAddUndo("Changed1");
            ChangeDataAndAddUndo("Changed2");            

            Assert.Equal("Changed2", data);
            UndoRedoService.PerformUndo();
            Assert.Equal("Changed1", data);
            UndoRedoService.PerformUndo();
            Assert.Equal("Init", data);
            UndoRedoService.PerformUndo();
            Assert.Equal("Init", data);

            UndoRedoService.PerformRedo();
            Assert.Equal("Changed1", data);
            UndoRedoService.PerformRedo();
            Assert.Equal("Changed2", data);
            UndoRedoService.PerformRedo();
            Assert.Equal("Changed2", data);

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void CanUndoWithRedoAction()
        {
            TestHelper.PrepareTestContext();

            string prevValue1 = data;
            string newValue1 = "Changed1";            
            UndoRedoService.DoWithUndo(() => data = newValue1, () => data = prevValue1);
            
            string prevValue2 = data;
            string newValue2 = "Changed2";
            UndoRedoService.DoWithUndo(() => data = newValue2, () => data = prevValue2);

            Assert.Equal("Changed2", data);
            UndoRedoService.PerformUndo();
            Assert.Equal("Changed1", data);
            UndoRedoService.PerformUndo();
            Assert.Equal("Init", data);

            UndoRedoService.PerformRedo();
            Assert.Equal("Changed1", data);
            UndoRedoService.PerformRedo();
            Assert.Equal("Changed2", data);

            UndoRedoService.PerformUndo();
            Assert.Equal("Changed1", data);
            UndoRedoService.PerformUndo();
            Assert.Equal("Init", data);

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void CanCombineUndoSteps()
        {
            TestHelper.PrepareTestContext();

            Assert.Equal("Init", data);
            ChangeDataAndAddUndo("Changed1");
            ChangeDataAndAddUndo("Changed2");

            Assert.True(UndoRedoService.TryCombineLastUndos(2));

            Assert.Equal("Changed2", data);
            UndoRedoService.PerformUndo();            
            Assert.Equal("Init", data);            

            UndoRedoService.PerformRedo();            
            Assert.Equal("Changed2", data);

            UndoRedoService.PerformUndo();
            Assert.Equal("Init", data);

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void CanClearUndoRedoSteps()
        {
            TestHelper.PrepareTestContext();

            Assert.Equal("Init", data);
            ChangeDataAndAddUndo("Changed1");
            ChangeDataAndAddUndo("Changed2");

            UndoRedoService.Clear();

            Assert.Equal("Changed2", data);

            UndoRedoService.PerformUndo();
            Assert.Equal("Changed2", data);            

            UndoRedoService.PerformRedo();
            Assert.Equal("Changed2", data);            

            Assert.False(TestHelper.HasAnyLogError());
        }




    }
}
