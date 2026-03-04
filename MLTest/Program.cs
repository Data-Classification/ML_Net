using static System.Console;
namespace MLTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                Clear();
                WriteLine("Enter text to analyze sentiment (or type 'exit' to quit):");
                WriteLine();
                string input = ReadLine();

                // Check if user wants to exit
                if (input?.ToLower() == "exit" || string.IsNullOrWhiteSpace(input))
                {
                    WriteLine("Exiting program...");
                    break;
                }

                var sampleData = new SentimentModel.ModelInput()
                {
                    Col0 = input!
                };

                // Load model and predict output of sample data
                var result = SentimentModel.Predict(sampleData);

                // If Prediction is 1, sentiment is "Positive"; otherwise, sentiment is "Negative"
                var sentiment = result.PredictedLabel == "1" ? "Positive" : "Negative";
                WriteLine($"Text: {sampleData.Col0}\nSentiment: {sentiment}");
                WriteLine(new string('-', 50));
                WriteLine();
                ReadKey();
            }
        }
    }
}
