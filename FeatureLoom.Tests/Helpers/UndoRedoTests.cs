using FeatureLoom.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace FeatureLoom.Helpers
{
    
    public class UndoRedoTests
    {

        [Fact]
        public void CanUndosAndRedosCanHaveDescriptions()        
        {
            TestHelper.PrepareTestContext();
            string data = "Init";            
            UndoRedoService.DoWithUndo(() => data = "Changed1", () => data = "Init", "Init->Changed1");
            UndoRedoService.DoWithUndo(() => data = "Changed2", () => data = "Changed1", "Changed1->Changed2");            

            Assert.Contains("Init->Changed1", UndoRedoService.UndoDescriptions);
            Assert.Contains("Changed1->Changed2", UndoRedoService.UndoDescriptions);
            Assert.Equal(2, UndoRedoService.UndoDescriptions.Count());
            Assert.Empty(UndoRedoService.RedoDescriptions);

            UndoRedoService.PerformUndo();

            Assert.Contains("Init->Changed1", UndoRedoService.UndoDescriptions);
            Assert.Contains("Changed1->Changed2", UndoRedoService.RedoDescriptions);
            Assert.Single(UndoRedoService.UndoDescriptions);
            Assert.Single(UndoRedoService.RedoDescriptions);

            UndoRedoService.PerformUndo();

            Assert.Contains("Init->Changed1", UndoRedoService.RedoDescriptions);
            Assert.Contains("Changed1->Changed2", UndoRedoService.RedoDescriptions);
            Assert.Empty(UndoRedoService.UndoDescriptions);
            Assert.Equal(2, UndoRedoService.RedoDescriptions.Count());

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void CanUndoAndRedoMultipleSteps()
        {
            TestHelper.PrepareTestContext();

            string data = "Init";
            UndoRedoService.DoWithUndo(() => data = "Changed1", () => data = "Init", "Init->Changed1");
            UndoRedoService.DoWithUndo(() => data = "Changed2", () => data = "Changed1", "Changed1->Changed2");

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
        public void CanPerformDoWithUndo()
        {
            TestHelper.PrepareTestContext();

            string data = "Init";

            UndoRedoService.DoWithUndo(() => data = "Changed1", () => data = "Init", "Init->Changed1");
            UndoRedoService.DoWithUndo(() => data = "Changed2", () => data = "Changed1", "Changed1->Changed2");

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

            string data = "Init";
            UndoRedoService.DoWithUndo(() => data = "Changed1", () => data = "Init", "Init->Changed1");
            UndoRedoService.DoWithUndo(() => data = "Changed2", () => data = "Changed1", "Changed1->Changed2");

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
        public void CanUseTransaction()
        {
            TestHelper.PrepareTestContext();

            string data = "Init";
            using (UndoRedoService.StartTransaction())
            {                
                UndoRedoService.DoWithUndo(() => data = "Changed1", () => data = "Init", "Init->Changed1");
                UndoRedoService.DoWithUndo(() => data = "Changed2", () => data = "Changed1", "Changed1->Changed2");
            }            

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

            string data = "Init";
            UndoRedoService.DoWithUndo(() => data = "Changed1", () => data = "Init", "Init->Changed1");
            UndoRedoService.DoWithUndo(() => data = "Changed2", () => data = "Changed1", "Changed1->Changed2");

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
