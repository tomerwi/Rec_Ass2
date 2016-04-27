using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace RecommenderSystem
{
    class RecommenderSystem
    {
        public enum PredictionMethod { Pearson, Cosine, Random, BaseModel, Stereotypes };

        //class members here
        private Dictionary<string, Dictionary<string, double>> m_ratings; //users to movies
        private Dictionary<string, double> m_userAvgs;
        private Dictionary<string, List<string>> movieToUser;
        private Dictionary<string, double> cosineDenominator;

        private Dictionary<string,Dictionary<string,double>> raiDic_AllUsers; //KEY = USERID. VALUE = <MOVIE,VAL> SO THAT VALUE IS THE DIFFERENCE (RATING-AVARAGE)
        private Dictionary<string, double> currentAvarage;

        //E2 fields
        private Dictionary<string, Dictionary<string, double>> m_ratings_train; //rating belongs to the train set
        private Dictionary<string, Dictionary<string, double>> m_ratings_test;
        private int dataSetSize = 0;

        //constructor
        public RecommenderSystem()
        {
            m_ratings = new Dictionary<string, Dictionary<string, double>>(); //<User <Movie,Rating>>
            m_userAvgs = new Dictionary<string, double>();
            movieToUser = new Dictionary<string, List<string>>();
            cosineDenominator = new Dictionary<string, double>();
            raiDic_AllUsers = new Dictionary<string, Dictionary<string, double>>();
            currentAvarage = new Dictionary<string, double>(); //not sure if we need it  

            //E2
            m_ratings_train = new Dictionary<string, Dictionary<string, double>>(); //<User <Movie,Rating>>
            m_ratings_test = new Dictionary<string, Dictionary<string, double>>(); //<User <Movie,Rating>>
        }

        //load a datatset 
        //The file contains one row for each u,i rating, in the following format:
        //userid::itemid::rating::timestamp
        //More at http://recsyswiki.com/wiki/Movietweetings
        //Download at https://github.com/sidooms/MovieTweetings/tree/master/latest
        //Do all precomputations here if needed
        public void Load(string sFileName)
        {
            try
            {
                using (FileStream fs = new FileStream(sFileName, FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader r = new StreamReader(fs, Encoding.UTF8))
                    {
                        parseRatings(r);
                        calcAvgs();
                        calcRAI();
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Couldn't load file");
            }
        }

        //new E2
        public void Load(string sFileName, double dTrainSetSize)
        {
            try
            {
                using (FileStream fs = new FileStream(sFileName, FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader r = new StreamReader(fs, Encoding.UTF8))
                    {
                        parseRatings(r);
                        splitToTrainAndTest(dTrainSetSize);
                        //calcAvgs();
                        //calcRAI();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't load file");
            }
        }
        /*
        private void parseRatings(StreamReader sr, double dTrainSetSize)
        {
            string line = sr.ReadLine();
            char[] sep = new char[1];
            sep[0] = ':';
            string formerUserID = "";

            while (line != null)
            {
                //char[] sep = new char[1];
                //sep[0] = ':';
                string[] l = line.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                if (l.Length == 4)
                {
                    string userId = l[0];
                    string movieId = l[1];
                    double rating = Double.Parse(l[2]);

                    if (formerUserID=="") //assign the userID in the first line
                        formerUserID = userId;


                    if (!m_ratings_train.ContainsKey(userId)) //new user
                    {
                        //Split the rating of the former user 
                        if (m_ratings.Keys.Count > 0)
                        {
                            splitToTrainAndTest(formerUserID, dTrainSetSize);
                            formerUserID = userId;
                        }
 

                        //Initiate Dictionaries
                        m_ratings.Add(userId, new Dictionary<string, double>());
                        m_ratings_test.Add(userId, new Dictionary<string, double>());
                        m_ratings_train.Add(userId, new Dictionary<string, double>());
                        //m_userAvgs.Add(userId, 0);
                    }
                    m_ratings[userId].Add(movieId, rating);
                }
                line = sr.ReadLine();
                //Split to train and test


            }
        }
        */

        private void splitToTrainAndTest(double dTrainSetSize)
        {
            HashSet<string> alreadyChosen = new HashSet<string>();
            int currentTestSize = 0;
            int dataSetSize = this.dataSetSize;
            Random r = new Random();
            int testSize = (int)((1 - dTrainSetSize) * dataSetSize);
           // float d = currentTestSize / dataSetSize;
            while (currentTestSize<testSize)
            {
                double currentNum_user = r.NextDouble();
                int locationOfUser = (int) ((m_ratings_train.Keys.Count - 1) * currentNum_user);
                if (locationOfUser < m_ratings_train.Keys.ToList().Count)
                {
                    string userID = m_ratings_train.Keys.ToList()[locationOfUser];
                    if (!alreadyChosen.Contains(userID))
                    {
                        int k = splitUserToTrainAndTest(userID, r.NextDouble());
                        currentTestSize += k;
                        alreadyChosen.Add(userID);
                    }
                }
            }
        }

        private int splitUserToTrainAndTest(string userID, double precentOfMoviesToTest) //returns number of movies moved to test set
        {
            int numOfMovies = m_ratings_train[userID].Keys.Count;
            int testSize =  (int) (numOfMovies * precentOfMoviesToTest);
            int currentTestSize = 0;
            m_ratings_test.Add(userID, new Dictionary<string, double>());
            
            foreach(string movieID in m_ratings[userID].Keys)
            {
                if (currentTestSize==testSize && (m_ratings_test.Count/dataSetSize)<0.05)
                    break;
                double rating = m_ratings[userID][movieID];
                m_ratings_test[userID].Add(movieID, rating);
                m_ratings_train[userID].Remove(movieID); 
                currentTestSize++;
            }
            if (m_ratings_train[userID].Count == 0)
                m_ratings_train.Remove(userID);
            return currentTestSize;
        }

        private void parseRatings(StreamReader sr) //not saving time stamp
        {
            string line = sr.ReadLine();
            while (line != null)
            {
                char[] sep = new char[1];
                sep[0] = ':';
                string[] l = line.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                if (l.Length == 4) 
                {
                    string userId = l[0];
                    string movieId = l[1];
                    double rating = Double.Parse(l[2]);
                    if (!m_ratings.ContainsKey(userId))
                    {
                        m_ratings.Add(userId, new Dictionary<string, double>());
                        m_ratings_train.Add(userId, new Dictionary<string, double>());
                        m_userAvgs.Add(userId, 0); 
                    }
                    m_ratings[userId].Add(movieId, rating);
                    m_ratings_train[userId].Add(movieId, rating);
                    m_userAvgs[userId] += rating;
                    dataSetSize++;

                    if (!cosineDenominator.ContainsKey(userId))
                    {
                        cosineDenominator.Add(userId, 0);
                    }
                    cosineDenominator[userId] = cosineDenominator[userId] + Math.Pow(rating,2);

                   if (!movieToUser.ContainsKey(movieId))
                    {
                        movieToUser.Add(movieId, new List<string>());

                    }
                    movieToUser[movieId].Add(userId); 

                    if (!currentAvarage.ContainsKey(userId))
                    {
                        currentAvarage.Add(userId, 0);
                    }
                    int currentRatedMoviesNum = m_ratings[userId].Keys.Count;
                    currentAvarage[userId] = currentAvarage[userId] + (rating / currentRatedMoviesNum);
                }
                line = sr.ReadLine();
            }
            sr.Close();
        }

        private void calcAvgs() //for the initial calculations ufter loading ratings file
        {
            //calc avg ratings for each user - the sums foreach user has already calculated
            foreach(string userId in m_ratings.Keys) //tomer:  maybe it can be done in the function above
            {
                int numOfRatings = m_ratings[userId].Count; //check!!
                double sumOfRatings = m_userAvgs[userId];
                m_userAvgs[userId] = sumOfRatings / numOfRatings;
            }
        }

        private void calcRAI()
        {
            foreach (string user in m_ratings.Keys)
            {
                double average = m_userAvgs[user];
                double value = 0;
                Dictionary<string, double> rai = new Dictionary<string, double>();
                foreach(string movie in m_ratings[user].Keys)
                {
                    value = m_ratings[user][movie]-average;
                    rai.Add(movie, value);
                    
                }
                raiDic_AllUsers.Add(user, rai);
            }
        }

        //return a list of the ids of all the users in the dataset
        public List<string> GetAllUsers()
        {
            return m_ratings.Keys.ToList();
        }

        //returns a list of all the items in the dataset
        public List<string> GetAllItems()
        {
            return movieToUser.Keys.ToList();
        }

        //returns the list of all items that the given user has rated in the dataset
        public List<string> GetRatedItems(string sUID)
        {
            if (m_ratings.ContainsKey(sUID))
            {
                return m_ratings[sUID].Keys.ToList();
            }
            else
                return null;//!!
        }

        //Returns a user-item rating that appears in the dataset (not predicted)
        public double GetRating(string sUID, string sIID)
        {
            if (m_ratings.ContainsKey(sUID))
            {
                double rating = -1;
                m_ratings[sUID].TryGetValue(sIID, out rating);
                return rating;
            }
            else
            {
                Console.WriteLine("User not found");
                return -1;
            }
        }

        //predict a rating for a user item pair using the specified method
        public double PredictRating(PredictionMethod m,string sUID, string sIID)
        {
            if (this.m_ratings.Count == 0)
            {
                Console.WriteLine("No ratings in memory");
                return -1;
            }
            if (!m_ratings.ContainsKey(sUID))
            {
                Console.WriteLine("invalid user ID");
                return -1;
            }
            if (!movieToUser.ContainsKey(sIID))
            {
                Console.WriteLine("invalid Item ID");
                return -1;
            }
            //if the user rated only one movie and the movie is sIID ? 
            if(m_ratings[sUID].Keys.Count==1 && m_ratings[sUID].ContainsKey(sIID))
            {
                return -1;
            }
            if(m== PredictionMethod.Cosine || m == PredictionMethod.Pearson)
            {
                double numerator = 0;
                double denominator = 0;
                foreach (string uID in movieToUser[sIID])
                {
                    if (uID.Equals(sUID))
                        continue;
                    double wau = 0;
                    double right = (m_ratings[uID][sIID] - m_userAvgs[uID]); //does it need to be Abs value??
                    if (m == PredictionMethod.Pearson)
                    {
                        wau = calcWPearson(sUID, uID, sIID);
                        if (wau >= 0.1)
                        {
                            numerator += (wau * right);
                            denominator += wau;
                        }
                    }
                    else if (m == PredictionMethod.Cosine)
                    {
                        wau = calcWCosine(sUID, uID, sIID);
                        numerator += (wau * right);
                        denominator += wau;
                    }

                }
                double maxRating = m_ratings[sUID].Values.Max();
                double ans = m_userAvgs[sUID];
                if (numerator == 0 && denominator == 0)
                    return ans;
                ans += (numerator / denominator);
                if (ans > maxRating) //!!
                    return maxRating;
                return ans; //should be Ra + num/dem
            }
            else//else random
            {
                return randomPredictRating(sUID,sIID);
            }
        }   

        private double randomPredictRating(string sUID, string sIID)//check this!
        {
            Random r = new Random();
            double random = r.NextDouble();
            List<double> uRatings = new List<double> (m_ratings[sUID].Values.ToList());
            if (m_ratings[sUID].ContainsKey(sIID))//if the user rated the movie don't consider it in the prediction
            {
                double rui = m_ratings[sUID][sIID];
                uRatings.Remove(rui);
            }
            int location = (int)(random * (uRatings.Count - 1));
            return uRatings[location];

        
        }
        private double calcWPearson(string aID, string uID, string sIID)
        {
            Dictionary<string, double> raiDic = raiDic_AllUsers[aID];
            Dictionary<string, double> ruiDic = raiDic_AllUsers[uID];
            double numerator = 0;
            double denominatorLeft = 0;
            double denominatorRight = 0;
            foreach(string mId in m_ratings[uID].Keys) 
            {
                if (!raiDic.ContainsKey(mId) || mId.Equals(sIID)) //only movies that they both rated and not take into account the movie that we want to predict
                    continue;
                double ruval = ruiDic[mId];
                double raval = raiDic[mId];
                numerator += (raiDic[mId] * ruiDic[mId]);
                denominatorLeft += Math.Pow(ruval, 2);
                denominatorRight+= Math.Pow(raval, 2);
            }
            double denominator = (Math.Sqrt(denominatorLeft)) * (Math.Sqrt(denominatorRight));
            if (denominator == 0) //throw exception?
                return 0; //check this
            return numerator/denominator;
        }

        private double calcWCosine(string aID, string uID, string sIID) 
        {
            double numerator = 0;
            foreach (string mId in m_ratings[uID].Keys)
            {
                double rui = m_ratings[uID][mId];
                if (!m_ratings[aID].ContainsKey(mId) || mId.Equals(sIID)) //not take into account the movie that we want to predict
                    continue;
                double rai = m_ratings[aID][mId];
                numerator += (rui * rai);
            }
            double denominator = (cosineDenominator[uID] * cosineDenominator[aID]);
            if (denominator == 0)
                return 0;
            return numerator / denominator;
        }

        //Compute MAE (mean absolute error) for a set of rating prediction methods over the same user-item pairs
        //cTrials specifies the number of user-item pairs to be tested
        public Dictionary<PredictionMethod, double> ComputeMAE(List<PredictionMethod> lMethods, int cTrials)
        {
            Dictionary<PredictionMethod, double> ans = new Dictionary<PredictionMethod, double>();
            if (this.m_ratings.Count == 0)
            {
                Console.WriteLine("No ratings in memory");
                return ans;
            }
            Dictionary<int, HashSet<int>> used = new Dictionary<int, HashSet<int>>();             
            int iterationNumber = 0;
            Random r = new Random();
            double pearsonMAE = 0;
            double cosineMAE = 0;
            double randomMAE = 0;
            while (iterationNumber<cTrials)
            {
                bool foundNotUsed = false;
                string userID = "";
                string movieID = "";
                while (!foundNotUsed)
                {
                    double randomU = r.NextDouble();
                    double randomI = r.NextDouble();
                    int locationU = (int)(randomU * (m_ratings.Keys.Count-1)); 
                    userID = m_ratings.Keys.ToList()[locationU]; //check if its better then ElementAt
                    int locationI = (int)(randomI * (m_ratings[userID].Keys.Count-1));
                    if (!used.ContainsKey(locationU))                   
                        used.Add(locationU, new HashSet<int>());
                    else if(used[locationU].Contains(locationI))
                            continue;
                    used[locationU].Add(locationI);
                    movieID = m_ratings[userID].Keys.ToList()[locationI]; 
                    foundNotUsed = true;
                }
                double realRating = m_ratings[userID][movieID];

                if (lMethods.Contains(PredictionMethod.Pearson))
                {
                    double pearsonRating = PredictRating(PredictionMethod.Pearson, userID, movieID);
                    if (pearsonRating == -1) //invalid user or movie
                        continue; 
                    double pearsonError = Math.Abs(realRating - pearsonRating); 
                    pearsonMAE += pearsonError;
                }

                if (lMethods.Contains(PredictionMethod.Cosine))
                {
                    double cosineRating = PredictRating(PredictionMethod.Cosine, userID, movieID);
                    if (cosineRating == -1)
                        continue;
                    double cosineError = Math.Abs(realRating - cosineRating);
                    cosineMAE += cosineError;
                }

                if (lMethods.Contains(PredictionMethod.Random))
                {
                    double randomRating = PredictRating(PredictionMethod.Random , userID, movieID);
                    if (randomRating == -1)
                        continue;
                    double randomError = Math.Abs(realRating - randomRating);
                    randomMAE += randomError;
                }

                iterationNumber++;
            }
            if(lMethods.Contains(PredictionMethod.Cosine))
                ans.Add(PredictionMethod.Cosine, cosineMAE / cTrials);
            if(lMethods.Contains(PredictionMethod.Pearson))
                ans.Add(PredictionMethod.Pearson, pearsonMAE / cTrials);
            if(lMethods.Contains(PredictionMethod.Random))
                ans.Add(PredictionMethod.Random, randomMAE / cTrials);
            return ans;
        }

        //new E2

        public void TrainBaseModel(int cFeatures)
        {
            double mue = computeMue(); //compute the avarage rating of all the users in the training data
            Random r = new Random();
            
        }

        private double computeMue() 
        {
            double totalRating  = 0;
            double numOfMovies = 0;
            foreach(string user in m_ratings_train.Keys)
            {
                foreach(string movie in m_ratings_train[user].Keys)
                {
                    totalRating = totalRating + m_ratings_train[user][movie];
                    numOfMovies++;
                }
            }

            return totalRating/numOfMovies;
        }
        public void TrainStereotypes(int cStereotypes)
        {
            throw new NotImplementedException();
        }

        public Dictionary<double, int> GetRatingsHistogram(string sUID)
        {
            throw new NotImplementedException();
        }


        //Compute RMSE on the train or on the test?
        public Dictionary<PredictionMethod, double> ComputeRMSE(List<PredictionMethod> lMethods, int cTrials)
        {
            Dictionary<PredictionMethod, double> ans = new Dictionary<PredictionMethod, double>();
            if (this.m_ratings.Count == 0)
            {
                Console.WriteLine("No ratings in memory");
                return ans;
            }
            Dictionary<int, HashSet<int>> used = new Dictionary<int, HashSet<int>>();
            int iterationNumber = 0;
            Random r = new Random();
            double pearsonMAE = 0;
            double cosineMAE = 0;
            double randomMAE = 0;
            while (iterationNumber < cTrials)
            {
                bool foundNotUsed = false;
                string userID = "";
                string movieID = "";
                while (!foundNotUsed)
                {
                    double randomU = r.NextDouble();
                    double randomI = r.NextDouble();
                    int locationU = (int)(randomU * (m_ratings.Keys.Count - 1));
                    userID = m_ratings.Keys.ToList()[locationU]; //check if its better then ElementAt
                    int locationI = (int)(randomI * (m_ratings[userID].Keys.Count - 1));
                    if (!used.ContainsKey(locationU))
                        used.Add(locationU, new HashSet<int>());
                    else if (used[locationU].Contains(locationI))
                        continue;
                    used[locationU].Add(locationI);
                    movieID = m_ratings[userID].Keys.ToList()[locationI];
                    foundNotUsed = true;
                }
                double realRating = m_ratings[userID][movieID];

                if (lMethods.Contains(PredictionMethod.Pearson))
                {
                    double pearsonRating = PredictRating(PredictionMethod.Pearson, userID, movieID);
                    if (pearsonRating == -1) //invalid user or movie
                        continue;
                    double pearsonError = Math.Pow(realRating - pearsonRating,2);
                    pearsonMAE += pearsonError;
                }

                if (lMethods.Contains(PredictionMethod.Cosine))
                {
                    double cosineRating = PredictRating(PredictionMethod.Cosine, userID, movieID);
                    if (cosineRating == -1)
                        continue;
                    double cosineError = Math.Pow(realRating - cosineRating,2);
                    cosineMAE += cosineError;
                }

                if (lMethods.Contains(PredictionMethod.Random))
                {
                    double randomRating = PredictRating(PredictionMethod.Random, userID, movieID);
                    if (randomRating == -1)
                        continue;
                    double randomError = Math.Pow(realRating - randomRating,2);
                    randomMAE += randomError;
                }

                iterationNumber++;
            }
            if (lMethods.Contains(PredictionMethod.Cosine))
                ans.Add(PredictionMethod.Cosine, cosineMAE / cTrials);
            if (lMethods.Contains(PredictionMethod.Pearson))
                ans.Add(PredictionMethod.Pearson, pearsonMAE / cTrials);
            if (lMethods.Contains(PredictionMethod.Random))
                ans.Add(PredictionMethod.Random, randomMAE / cTrials);
            return ans;
        }
    }
}
