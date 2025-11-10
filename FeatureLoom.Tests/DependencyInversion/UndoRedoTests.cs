using FeatureLoom.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using FeatureLoom.UndoRedo;
using FeatureLoom.Helpers;

namespace FeatureLoom.UndoRedo
{
    
    public class UndoRedoTests
    {

        [Fact]
        public void CanUndosAndRedosCanHaveDescriptions()        
        {
            using var testContext = TestHelper.PrepareTestContext();
            
            var undoRedo = new Helpers.UndoRedo();
            undoRedo.DoWithUndo(() => { }, () => { }, "Init->Changed1");
            undoRedo.DoWithUndo(() => { }, () => { }, "Changed1->Changed2");            

            Assert.Contains("Init->Changed1", undoRedo.UndoDescriptions);
            Assert.Contains("Changed1->Changed2", undoRedo.UndoDescriptions);
            Assert.Equal(2, undoRedo.UndoDescriptions.Count());
            Assert.Empty(undoRedo.RedoDescriptions);

            undoRedo.PerformUndo();

            Assert.Contains("Init->Changed1", undoRedo.UndoDescriptions);
            Assert.Contains("Changed1->Changed2", undoRedo.RedoDescriptions);
            Assert.Single(undoRedo.UndoDescriptions);
            Assert.Single(undoRedo.RedoDescriptions);

            undoRedo.PerformUndo();

            Assert.Contains("Init->Changed1", undoRedo.RedoDescriptions);
            Assert.Contains("Changed1->Changed2", undoRedo.RedoDescriptions);
            Assert.Empty(undoRedo.UndoDescriptions);
            Assert.Equal(2, undoRedo.RedoDescriptions.Count());

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void CanUndoAndRedoMultipleSteps()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var undoRedo = new Helpers.UndoRedo();
            string data = "Init";
            undoRedo.DoWithUndo(() => data = "Changed1", () => data = "Init", "Init->Changed1");
            undoRedo.DoWithUndo(() => data = "Changed2", () => data = "Changed1", "Changed1->Changed2");

            Assert.Equal("Changed2", data);
            undoRedo.PerformUndo();
            Assert.Equal("Changed1", data);
            undoRedo.PerformUndo();
            Assert.Equal("Init", data);
            undoRedo.PerformUndo();
            Assert.Equal("Init", data);

            undoRedo.PerformRedo();
            Assert.Equal("Changed1", data);
            undoRedo.PerformRedo();
            Assert.Equal("Changed2", data);
            undoRedo.PerformRedo();
            Assert.Equal("Changed2", data);

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void CanPerformDoWithUndo()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var undoRedo = new Helpers.UndoRedo();
            string data = "Init";

            undoRedo.DoWithUndo(() => data = "Changed1", () => data = "Init", "Init->Changed1");
            undoRedo.DoWithUndo(() => data = "Changed2", () => data = "Changed1", "Changed1->Changed2");

            Assert.Equal("Changed2", data);
            undoRedo.PerformUndo();
            Assert.Equal("Changed1", data);
            undoRedo.PerformUndo();
            Assert.Equal("Init", data);

            undoRedo.PerformRedo();
            Assert.Equal("Changed1", data);
            undoRedo.PerformRedo();
            Assert.Equal("Changed2", data);

            undoRedo.PerformUndo();
            Assert.Equal("Changed1", data);
            undoRedo.PerformUndo();
            Assert.Equal("Init", data);

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void CanCombineUndoSteps()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var undoRedo = new Helpers.UndoRedo();
            string data = "Init";
            undoRedo.DoWithUndo(() => data = "Changed1", () => data = "Init", "Init->Changed1");
            undoRedo.DoWithUndo(() => data = "Changed2", () => data = "Changed1", "Changed1->Changed2");

            Assert.True(undoRedo.TryCombineLastUndos(2));

            Assert.Equal("Changed2", data);
            undoRedo.PerformUndo();            
            Assert.Equal("Init", data);

            undoRedo.PerformRedo();            
            Assert.Equal("Changed2", data);

            undoRedo.PerformUndo();
            Assert.Equal("Init", data);

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void CanUseTransaction()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var undoRedo = new Helpers.UndoRedo();
            string data = "Init";
            using (undoRedo.StartTransaction())
            {
                undoRedo.DoWithUndo(() => data = "Changed1", () => data = "Init", "Init->Changed1");
                undoRedo.DoWithUndo(() => data = "Changed2", () => data = "Changed1", "Changed1->Changed2");
            }            

            Assert.Equal("Changed2", data);
            undoRedo.PerformUndo();
            Assert.Equal("Init", data);

            undoRedo.PerformRedo();
            Assert.Equal("Changed2", data);

            undoRedo.PerformUndo();
            Assert.Equal("Init", data);

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void CanClearUndoRedoSteps()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var undoRedo = new Helpers.UndoRedo();
            string data = "Init";
            undoRedo.DoWithUndo(() => data = "Changed1", () => data = "Init", "Init->Changed1");
            undoRedo.DoWithUndo(() => data = "Changed2", () => data = "Changed1", "Changed1->Changed2");

            undoRedo.Clear();

            Assert.Equal("Changed2", data);

            undoRedo.PerformUndo();
            Assert.Equal("Changed2", data);

            undoRedo.PerformRedo();
            Assert.Equal("Changed2", data);            

            Assert.False(TestHelper.HasAnyLogError());
        }




    }
}
