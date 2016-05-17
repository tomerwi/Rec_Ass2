using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//add here other usings that were added to E2

namespace RecommenderSystem
{
    class Program
    {
        static void Assignment1()
        {
            //Create the recommender system and load the dataset
            RecommenderSystem rs = new RecommenderSystem();
            rs.Load("rating.dat");

            //verify that the dataset was properly loaded
            Console.WriteLine("The predicted rating of user 17678 for item 2101441 is " + rs.GetRating("17678", "2101441"));

            //Test the prediction algorithms
            Console.WriteLine("Predicted rating of user 26291 to item 4535650 using Pearson is " +
                Math.Round(rs.PredictRating(RecommenderSystem.PredictionMethod.Pearson, "26291", "4535650"), 4));
            Console.WriteLine("Predicted rating of user 32399 to item 0095953 using Cosine is " +
                Math.Round(rs.PredictRating(RecommenderSystem.PredictionMethod.Cosine, "32399", "0095953"), 4));
            Console.WriteLine("Predicted rating of user 9434 to item 1321870 using Random is " +
                Math.Round(rs.PredictRating(RecommenderSystem.PredictionMethod.Random, "9434", "1321870"), 4));

            //Compute MAE over the same u,i pairs for a set of algorithms
            List<RecommenderSystem.PredictionMethod> lMethods = new List<RecommenderSystem.PredictionMethod>();
            lMethods.Add(RecommenderSystem.PredictionMethod.Pearson);
            lMethods.Add(RecommenderSystem.PredictionMethod.Cosine);
            lMethods.Add(RecommenderSystem.PredictionMethod.Random);
            DateTime dtStart = DateTime.Now;
            Dictionary<RecommenderSystem.PredictionMethod, double> dResults = rs.ComputeMAE(lMethods, 1000);
            Console.WriteLine("MAE scores for Pearson, Cosine, and Random are:");
            foreach (KeyValuePair<RecommenderSystem.PredictionMethod, double> p in dResults)
                Console.Write(p.Key + "=" + Math.Round(p.Value, 4) + ", ");

            Console.WriteLine();
            Console.WriteLine("MAE computation time was " + Math.Round((DateTime.Now - dtStart).TotalSeconds, 0));
        }

        static void Assignment2()
        {
            RecommenderSystem rs = new RecommenderSystem();
            rs.Load("C:\\Users\\Tomer\\Documents\\GitHub\\Rec_Ass2\\ratings.dat", 0.95);
            //rs.Load("ratings.dat", 0.95);


           // rs.TrainBaseModel(10);
           // rs.TrainStereotypes(10);
            List<RecommenderSystem.PredictionMethod> lMethods = new List<RecommenderSystem.PredictionMethod>();
            lMethods.Add(RecommenderSystem.PredictionMethod.BaseModel);
            lMethods.Add(RecommenderSystem.PredictionMethod.Stereotypes);
            lMethods.Add(RecommenderSystem.PredictionMethod.Pearson);
            lMethods.Add(RecommenderSystem.PredictionMethod.Cosine);
            lMethods.Add(RecommenderSystem.PredictionMethod.Random);
            DateTime dtStart = DateTime.Now;
            Dictionary<RecommenderSystem.PredictionMethod, double> dResults = rs.ComputeRMSE(lMethods,10); 
            Console.WriteLine("Hit ratio scores for Pearson, Cosine, BaseModel, Stereotypes, and Random are:");
            foreach (KeyValuePair<RecommenderSystem.PredictionMethod, double> p in dResults)
                Console.Write(p.Key + "=" + Math.Round(p.Value, 4) + ", ");
            Console.WriteLine();
            Console.WriteLine("Execution time was " + Math.Round((DateTime.Now - dtStart).TotalSeconds, 0));
            Console.ReadLine();
            
        }


        static void Main(string[] args)
        {
            //An example of the required capabilities in assignment 1
           // Assignment1();
            //Console.ReadLine();

            Assignment2();
        }
    }
}
