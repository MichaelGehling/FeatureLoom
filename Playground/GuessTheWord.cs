using FeatureLoom.Logging;
using FeatureLoom.Workflows;
using System;
using System.Collections.Generic;

namespace Playground
{
    public class GuessTheWord : Workflow<GuessTheWord.StateMachine>
    {
        private static void Main()
        {
            var guessTheWord = new GuessTheWord();
            guessTheWord.ExecutionInfoSource.ConnectTo(new WorkflowExecutionInfoLogger());
            guessTheWord.Run();
            guessTheWord.WaitUntilFinished();
        }

        private string theWord;
        private List<char> guessedChars = new List<char>();
        private int counter = 0;
        private bool done = false;

        public class StateMachine : StateMachine<GuessTheWord>
        {
            protected override void Init()
            {
                var entering = State("entering");
                var guessing = State("guessing");
                var finished = State("finished");

                entering.Build()
                    .Step("Prepare the screen")
                        .Do(c =>
                        {
                            Console.Clear();
                            Console.Write("Enter the word: ");
                        })
                    .Step("Enter the word")
                        .Do(c => c.theWord = Console.ReadLine())
                    .Step("Start guessing if the word is valid, otherwise start over, again")
                        .If(c => c.theWord.Length >= 2)
                            .Goto(guessing)
                        .Else()
                            .Loop();

                guessing.Build()
                    .Step("Prepare the screen")
                        .Do(c =>
                        {
                            Console.Clear();
                            Console.WriteLine($"You guessed {c.counter} {(c.counter == 1 ? "character" : "characters")} wrong!");
                            Console.Write("Guess the word: ");

                            for (int i = 0; i < c.theWord.Length; i++)
                            {
                                if (c.guessedChars.Contains(Char.ToLower(c.theWord[i]))) Console.Write(c.theWord[i]);
                                else Console.Write('_');
                            }
                        })
                    .Step("Guess a character")
                        .Do(c =>
                        {
                            char guessedChar = Char.ToLower(Console.ReadKey().KeyChar);
                            c.guessedChars.Add(guessedChar);
                            if (!c.theWord.ToLower().Contains(guessedChar)) c.counter++;

                            c.done = true;
                            for (int i = 0; i < c.theWord.Length; i++)
                            {
                                if (!c.guessedChars.Contains(Char.ToLower(c.theWord[i])))
                                {
                                    c.done = false;
                                    break;
                                }
                            }
                        })
                    .Step("If the word is guessed finish, else try again")
                        .If(c => c.done)
                            .Goto(finished)
                        .Else()
                            .Loop();

                finished.Build()
                    .Step("Victory Screen")
                        .Do(c =>
                        {
                            Console.Clear();
                            Console.WriteLine($"You did it! {c.theWord} ");
                            Console.WriteLine($"You guessed {c.counter} {(c.counter == 1 ? "character" : "characters")} wrong!");
                            Console.Write("Do you want to play again?");
                            c.done = Char.ToLower(Console.ReadKey().KeyChar) != 'y';
                        })
                    .Step("Restart if player wants to play again, otherwinse finish")
                        .If(c => !c.done)
                            .Do(c =>
                            {
                                c.counter = 0;
                                c.guessedChars.Clear();
                            })
                            .Goto(entering)
                        .Else()
                            .Finish();
            }
        }
    }
}