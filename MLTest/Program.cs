using static System.Console;
namespace MLTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            WriteLine("Enter text to analyze sentiment:");
            WriteLine();
            string input = ReadLine();
            var sampleData = new SentimentModel.ModelInput()
            {
                
                Col0 = input
            };

            // Load model and predict output of sample data
            var result = SentimentModel.Predict(sampleData);

            // If Prediction is 1, sentiment is "Positive"; otherwise, sentiment is "Negative"
            var sentiment = result.PredictedLabel == "1" ? "Positive" : "Negative";
            WriteLine($"Text: {sampleData.Col0}\nSentiment: {sentiment}");
        }
    }
}
