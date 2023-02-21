using FeatureLoom.Logging;
using FeatureLoom.Statemachines;
using FeatureLoom.Workflows;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Playground
{
    public class GuessTheWord
    {
        private string theWord;
        private int counter = 0;

        private static async Task Main()
        {
            Statemachine<GuessTheWord> statemachine = new Statemachine<GuessTheWord>(
                new Statemachine<GuessTheWord>.State("entering", async (c, token) =>
                {
                    while (true)
                    {
                        Console.Clear();
                        Console.Write("Enter the word: ");
                        c.theWord = Console.ReadLine();
                        if (c.theWord.Length >= 2) return "guessing";
                    }
                }),
                new Statemachine<GuessTheWord>.State("guessing", async (c, token) =>
                {
                    List<char> guessedChars = new List<char>();
                    while (true)
                    {
                        Console.Clear();
                        Console.WriteLine($"You guessed {c.counter} {(c.counter == 1 ? "character" : "characters")} wrong!");
                        Console.Write("Guess the word: ");

                        for (int i = 0; i < c.theWord.Length; i++)
                        {
                            if (guessedChars.Contains(Char.ToLower(c.theWord[i]))) Console.Write(c.theWord[i]);
                            else Console.Write('_');
                        }

                        char guessedChar = Char.ToLower(Console.ReadKey().KeyChar);
                        guessedChars.Add(guessedChar);
                        if (!c.theWord.ToLower().Contains(guessedChar)) c.counter++;

                        bool done = true;
                        for (int i = 0; i < c.theWord.Length; i++)
                        {
                            if (!guessedChars.Contains(Char.ToLower(c.theWord[i])))
                            {
                                done = false;
                                break;
                            }
                        }
                        if (done) return "finished";
                    }
                }),
                new Statemachine<GuessTheWord>.State("finished", async (c, token) =>
                {
                    Console.Clear();
                    Console.WriteLine($"You did it! {c.theWord} ");
                    Console.WriteLine($"You guessed {c.counter} {(c.counter == 1 ? "character" : "characters")} wrong!");
                    Console.Write("Do you want to play again?");
                    bool playAgain = Char.ToLower(Console.ReadKey().KeyChar) == 'y';
                    if (playAgain)
                    {
                        c.counter = 0;
                        return "entering";
                    }
                    else return "";
                }));

            await statemachine.CreateAndStartJob(new GuessTheWord());            
        }

    }
}