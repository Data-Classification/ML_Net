using static System.Console;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
namespace MLTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var sampleData = new SentimentModel.ModelInput()
            {
                Col0 = "Youtube is so awful."
            };

            // Load model and predict output of sample data
            // quang khai báo
            // nguyen test
            // test hieu
            //test thien1
            var result = SentimentModel.Predict(sampleData);

            // If Prediction is 1, sentiment is "Positive"; otherwise, sentiment is "Negative"
            var sentiment = result.PredictedLabel == 1 ? "Positive" : "Negative";
            Console.WriteLine($"Text: {sampleData.Col0}\nSentiment: {sentiment}");
        }
    }
}
