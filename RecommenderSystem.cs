using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

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
       // private Dictionary<string, Dictionary<string, double>> m_ratings_trainOfTrain;
        private Dictionary<string, Dictionary<string, double>> m_ratings_validation;
        private Dictionary<string, Dictionary<string, double>> m_centroids;
        private Dictionary<string, double> m_centroidAvg;
        //private Dictionary<string, Dictionary<string, double>> m_rui_base_model;
        private Dictionary<string, double> buDic;
        private Dictionary<string, double> biDic;
        private Dictionary<string, List<double>> puDic;
        private Dictionary<string, List<double>> qiDic;
        private double mue;
        private int dataSetSize = 0;
        bool trainedBaseModel = false;
        bool trainedStereoType = false;

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
            //m_ratings_trainOfTrain = new Dictionary<string, Dictionary<string, double>>();
            m_ratings_validation = new Dictionary<string, Dictionary<string, double>>();
            m_centroids = new Dictionary<string, Dictionary<string, double>>();
            m_centroidAvg = new Dictionary<string, double>();
            //m_rui_base_model = new Dictionary<string, Dictionary<string, double>>();
            buDic = new Dictionary<string, double>(); //string = userID, value = bu
            biDic = new Dictionary<string, double>();

            puDic = new Dictionary<string, List<double>>(); //I think that each pu and qi is a vector of values
            qiDic = new Dictionary<string, List<double>>();
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
                        mue = computeMue(); //it computes the mue only on the train
                        calcAvgs(); //it computes the avrage on m_ratings 
                        calcRAI();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't load file");
            }
        }

        private void splitToTrainAndTest(double dTrainSetSize)
        {
            HashSet<string> alreadyChosen = new HashSet<string>();
            int currentTestSize = 0;
            Random r = new Random();
            int testSize = (int)((1 - dTrainSetSize) * dataSetSize);
            //int validationSize = testSize;
            bool validation = false;
            // float d = currentTestSize / dataSetSize;
            while (currentTestSize < testSize || !validation) 
            {
                if (currentTestSize >= testSize)
                {
                    validation = true;
                    currentTestSize = 0;
                }
                double currentNum_user = r.NextDouble();
                int locationOfUser = (int)((m_ratings_train.Keys.Count - 1) * currentNum_user);
                if (locationOfUser < m_ratings_train.Keys.ToList().Count) //tomer: this conditions cant be false
                {
                    string userID = m_ratings_train.Keys.ToList()[locationOfUser];
                    if (!alreadyChosen.Contains(userID) && m_ratings_train[userID].Keys.Count>1)
                    {
                        int k = splitUserToTrainAndTest(userID, r.NextDouble(),validation);
                        if (k > 0) //only if we actually took some movies from this user
                        {
                            currentTestSize += k;
                            alreadyChosen.Add(userID);
                        }
                    }
                }
            }
        }

        private int splitUserToTrainAndTest(string userID, double precentOfMoviesToTest, bool validation) //returns number of movies moved to test set
        {
            int numOfMovies = (m_ratings_train[userID].Keys.Count-1);
            int k = (int)(numOfMovies * precentOfMoviesToTest);
            int numOfAdded = 0;
            if (numOfAdded < k)
            {
                if (!validation)
                    m_ratings_test.Add(userID, new Dictionary<string, double>());
                else
                    m_ratings_validation.Add(userID, new Dictionary<string, double>());
            }

            foreach (string movieID in m_ratings[userID].Keys)
            {
                //int precentOfTest = m_ratings_test.Count / dataSetSize; //1) always be zero because its int. 2) m_rating.count gives the number of users, not the number of Dataset!
                if (numOfAdded >= k /*precentOfTest > 0.05*/)
                    break;
                double rating = m_ratings[userID][movieID];
                if(!validation)
                    m_ratings_test[userID].Add(movieID, rating);
                else
                    m_ratings_validation[userID].Add(movieID, rating);
                m_ratings_train[userID].Remove(movieID);
                numOfAdded++;
            }
            if (m_ratings_train[userID].Count == 0) //cant be true
                m_ratings_train.Remove(userID);
            return numOfAdded;
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
            if (m == PredictionMethod.BaseModel)
            {
                if (!trainedBaseModel)
                {
                    TrainBaseModel(10); //we need to save bu bi qu pi as fields in order to get the rating
                    trainedBaseModel = true;
                }
                return predictRatingBaseModel(sUID, sIID);

            }
            if (m == PredictionMethod.Stereotypes)
            {
                if (!trainedStereoType)
                {
                    TrainStereotypes(10);
                    trainedStereoType = true;
                }
                return predictRatingStereoType(sUID, sIID);
                

            }
            else//else random
            {
                return randomPredictRating(sUID,sIID);
            }


        }   
        
        private double predictRatingStereoType(string userId, string itemId)
        {
            //Find the closest centroid
            if(m_centroids.Count ==0)
                return m_userAvgs[userId];
            double bestDis = 0;
            string bestCentroid = null;
            foreach(string centroid in m_centroids.Keys)
            {
                double dis = calcUserCentroidPearson(userId, centroid);
                if (bestCentroid == null || bestDis < dis) //if we are in the first iteration or we found a closer centroid
                {
                    bestDis = dis;
                    bestCentroid = centroid;
                }
            }
            if(bestCentroid ==null)
                return m_userAvgs[userId];
            //what if the chosen centroid doesnt have the item?? return the avg of the cent? avg of the user? or in the loop above check only the centroids that have the item?
            if (m_centroids[bestCentroid].ContainsKey(itemId))
                return m_centroids[bestCentroid][itemId];
            else
                return m_centroidAvg[bestCentroid];
        }

        private double predictRatingBaseModel(string userId, string itemId)
        {
            double bi = 0;
            if(biDic.ContainsKey(itemId))
                bi = biDic[itemId];

            double bu = 0;
            if(buDic.ContainsKey(userId))
                bu = buDic[userId];
            double puqi = 0;//hilla
            if(puDic.ContainsKey(userId) && qiDic.ContainsKey(itemId))
            {
                for (int i = 0; i < puDic[userId].Count && i< qiDic[itemId].Count; i++)//hilla
                {
                    puqi += (puDic[userId][i] * qiDic[itemId][i]);
                }
            }
            double rui = mue + bi + bu + puqi;
            if (rui == 0)
                return m_userAvgs[userId];
            return rui;
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

        private double getSmallRandomNumber()
        {
            Random random = new Random();
            bool negative = random.NextDouble() > 0.5;
            double r = random.NextDouble();
            r = r * (0.001);
            if (negative)
                return (-1 * r);
            else
                return r;
        }
        //new E2
        public void TrainBaseModel(int cFeatures)
        {
            buDic = new Dictionary<string, double>();
            biDic = new Dictionary<string, double>();

            puDic = new Dictionary<string, List<double>>(); 
            qiDic = new Dictionary<string, List<double>>();

            foreach (string user in m_ratings.Keys)
            {
                buDic.Add(user, getSmallRandomNumber());
                puDic.Add(user, new List<double>());
                int counter = 0;
                while (counter < cFeatures)
                {
                    puDic[user].Add(getSmallRandomNumber());
                    counter++;
                }
                
            }

            foreach(string item in movieToUser.Keys)
            {
                biDic.Add(item, getSmallRandomNumber());
                qiDic.Add(item, new List<double>());
                int counter = 0;
                while (counter < cFeatures)
                {
                    qiDic[item].Add(getSmallRandomNumber());
                    counter++;
                }
            }
            double gamma = 0.01;
            double lamda = 0.05;

            double bestRMSE = Double.MaxValue;
             
            bool eValidationStillImproving = true;
            while (eValidationStillImproving) //while we are still improving
            {
                foreach (string userID in m_ratings_train.Keys)
                {
                    foreach (string movieID in m_ratings_train[userID].Keys)
                    {
                        double rui = m_ratings_train[userID][movieID];
                        double bi = biDic[movieID];
                        double bu = buDic[userID];
                        double puqi = 0;
                        for(int i = 0; i < cFeatures; i++)
                        {
                            puqi += (puDic[userID][i] * qiDic[movieID][i]);
                        }
                        double eui = rui - mue - bi - bu - puqi;

                        //update parameters:
                        buDic[userID] = bu + (gamma * (eui - (lamda * bu)));
                        biDic[movieID] = bi + (gamma * (eui - (lamda * bi)));
                        for (int i = 0; i < cFeatures; i++)//hilla
                        {
                            puDic[userID][i] = puDic[userID][i] + (gamma * ((eui* qiDic[movieID][i]) - (lamda * puDic[userID][i]))); 
                            qiDic[movieID][i] = qiDic[movieID][i] + (gamma * ((eui* puDic[userID][i]) - (lamda * qiDic[movieID][i])));
                        }
                    }
                }
                //compute validation error
                double n = 0;
                double currentE_Squre = 0;
                
                foreach (string userID in m_ratings_validation.Keys)
                {
                    foreach (string movieID in m_ratings_validation[userID].Keys)
                    {
                        double rui = m_ratings_validation[userID][movieID];
                        double bi = biDic[movieID];
                        double bu = buDic[userID];
                        // double pu = 0.04; //where can we get this number?
                        //  double qi = 0.05; //where can we get this number?
                        double puqi = 0;//hilla
                        for (int i = 0; i < cFeatures; i++)//hilla
                        {
                            puqi += (puDic[userID][i] * qiDic[movieID][i]);
                        }
                        double eui = rui - mue - bi - bu - puqi;

                        //for RMSE
                        double euiSqure = Math.Pow(eui, 2);
                        currentE_Squre = +euiSqure;
                        n++;
                    }
                }

                //For RMSE
                double RMSE = Math.Sqrt(currentE_Squre/n);
                if (RMSE > bestRMSE)//error wasnt imporved, then we dont do another iteration
                    eValidationStillImproving = false;
                else
                    bestRMSE = RMSE;
            }
            trainedBaseModel = true;
        }

        private double computeMue() 
        {
            double totalRating  = 0;
            double numOfMovies = 0;
            foreach(string user in m_ratings.Keys)
            {
                foreach(string movie in m_ratings[user].Keys)
                {
                    totalRating = totalRating + m_ratings[user][movie];
                    numOfMovies++;
                }
            }

            return totalRating/numOfMovies;
        }
        private double calcUserCentroidPearson(string aID, string centroidID)
        {
            double numerator = 0;
            double denominatorLeft = 0;
            double denominatorRight = 0;
            double centroidAvg = m_centroidAvg[centroidID];
            foreach (string itemID in raiDic_AllUsers[aID].Keys) 
            {
                if (!m_centroids[centroidID].ContainsKey(itemID)) //only movies that they both rated and not take into account the movie that we want to predict
                    continue;
                double ruval = m_centroids[centroidID][itemID] - centroidAvg;
                double raval = raiDic_AllUsers[aID][itemID];
                numerator += (raval * ruval);
                denominatorLeft += Math.Pow(ruval, 2);
                denominatorRight += Math.Pow(raval, 2);
            }
            double denominator = (Math.Sqrt(denominatorLeft)) * (Math.Sqrt(denominatorRight));
            if (denominator == 0) //throw exception?
                return 0; //check this
            return numerator / denominator;
        }
        public void TrainStereotypes(int cStereotypes)
        {
            Dictionary<string, string> initialUsers = new Dictionary<string, string>(); //key = centorid. value = userID which first created the centroid
            //choosing random users as initial centorids
            Stopwatch stopwatch = new Stopwatch();
            TimeSpan timeout = new TimeSpan(0, 5, 0);
            stopwatch.Start();
            if (m_centroids.Count > 0)
                m_centroids.Clear();
            if (m_centroidAvg.Count > 0)
                m_centroidAvg.Clear();
            Dictionary<string, Dictionary<string, List<double>>> centroidsTemp = new Dictionary<string, Dictionary<string, List<double>>>();
            Random r = new Random();
            int numOfUsersInTrain = m_ratings_train.Keys.Count -1;
            
            while (m_centroids.Keys.Count < cStereotypes)
            {
                double random = r.NextDouble();
                int location = (int) (random * numOfUsersInTrain);
                string userID = m_ratings_train.Keys.ToList()[location];
                if (!m_centroids.ContainsKey(userID))
                {
                    
                    //check that the centroids are different from each other
                    bool tooCloseToAnotherCentroid = false;
                    foreach(string user in m_centroids.Keys)
                    {
                        double pearson = calcWPearson(userID, user, "");
                        if (pearson > 0.4) //check this number!!!!!
                        {
                            tooCloseToAnotherCentroid = true;
                            break;
                        }
                    }
                    if(tooCloseToAnotherCentroid)
                        continue;
                    
                    m_centroids.Add(userID, new Dictionary<string, double>());
                    m_centroidAvg.Add(userID, m_userAvgs[userID]);
                    centroidsTemp.Add(userID, new Dictionary<string, List<double>>());
                    foreach(string itemID in m_ratings_train[userID].Keys)
                    {
                        m_centroids[userID].Add(itemID, m_ratings_train[userID][itemID]); //add the rating to the centroid
                        centroidsTemp[userID].Add(itemID, new List<double>()); //every item can have more than one rating (by many users)
                        centroidsTemp[userID][itemID].Add(m_ratings_train[userID][itemID]); //add rating to the item
                    }

                }
            }
            
            bool toContinue = true;
            while (toContinue)
            {
                //Computing distance of users to centroids
                foreach (string userID in m_ratings_train.Keys)
                {
                    if (m_centroids.ContainsKey(userID))
                        continue;
                    double bestDis = 0; //best distance is the max and not min (pearson value)
                    string bestCentroid = null;
                    foreach (string centroid in m_centroids.Keys)
                    {
                        //Compute perason distance from user to each centroid and attach him to the closest one
                        double dis = calcUserCentroidPearson(userID, centroid);
                        if (bestCentroid == null || bestDis < dis) //if we are in the first iteration or we found a closer centroid
                        {
                            bestDis = dis;
                            bestCentroid = centroid;
                        }
                    }
                    if (bestCentroid != null) 
                    {
                        foreach (string itemID in m_ratings_train[userID].Keys)
                        {
                            if (!centroidsTemp[bestCentroid].ContainsKey(itemID))
                                centroidsTemp[bestCentroid].Add(itemID, new List<double>());
                            centroidsTemp[bestCentroid][itemID].Add(m_ratings_train[userID][itemID]);
                            //tomer: maybe here we need to remove the movies of the user from the former centorid
                        }

                    }
                }
                bool centGood = true;
                //calc the avg of the cent
                foreach (string centroid in centroidsTemp.Keys)
                {
                    double centSum = 0;
                    int centCount = 0;
                    double centOldAvg = m_centroidAvg[centroid];

                    foreach (string item in centroidsTemp[centroid].Keys)
                    {
                        double centItemSum = centroidsTemp[centroid][item].Sum();
                        int centItemCount = centroidsTemp[centroid][item].Count;
                        centCount += centItemCount;
                        centSum += centItemSum;
                    }
                    double centNewAvg = (centSum / centCount);
                    m_centroidAvg[centroid] = centNewAvg;
                    double numeratorPearson = 0;
                    double denominatorLeftPearson = 0;
                    double denominatorRightPearson = 0;
                    foreach (string item in centroidsTemp[centroid].Keys)
                    {
                        double centItemSum = centroidsTemp[centroid][item].Sum();
                        int centItemCount = centroidsTemp[centroid][item].Count;
                        double newri = (centItemSum / centItemCount);
                        if (!m_centroids[centroid].ContainsKey(item)) 
                            m_centroids[centroid].Add(item, newri);
                        else
                        {
                            //calc pearson between cents
                            if (centGood)
                            {
                                double oldrval = m_centroids[centroid][item] - centOldAvg;
                                double newrval = newri - centNewAvg;
                                numeratorPearson += (oldrval * newrval);
                                denominatorLeftPearson += Math.Pow(oldrval, 2);
                                denominatorRightPearson += Math.Pow(newrval, 2);
                            }
                            m_centroids[centroid][item] = newri;
                        }
                    }
                    if (centGood)
                    {
                        double denominatorPearson = (Math.Sqrt(denominatorLeftPearson)) * (Math.Sqrt(denominatorRightPearson));
                        if (denominatorPearson == 0)
                            centGood = true;
                        else
                        {
                            double pearsonVal = numeratorPearson / denominatorPearson;
                            if (pearsonVal < 0.95) //check vals!
                                centGood = false;
                        }
                    }

                }
                centroidsTemp.Clear();
                foreach (string cent in m_centroids.Keys)
                {
                    centroidsTemp.Add(cent, new Dictionary<string, List<double>>());
                    foreach(string itemID in m_centroids[cent].Keys)// need to iterate only on the initial movies of the user when we first created the centorid. movies of other users can be added and it will throw exepetion
                    {
                        centroidsTemp[cent].Add(itemID, new List<double>());
                        centroidsTemp[cent][itemID].Add(m_centroids[cent][itemID]); //maybe remove this
                    }

                }
                if (centGood)// we can stop
                {
                    toContinue = false;
                }
                else if(stopwatch.Elapsed>timeout)
                {
                    toContinue = false;
                }

            }
            trainedStereoType = true;     

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
            double pearsonRMSE = 0;
            double cosineRMSE = 0;
            double randomRMSE = 0;
            double baseModelRMSE = 0;
            double stereoTypeRMSE = 0;
            foreach(string userID in m_ratings_test.Keys)
            {
                foreach (string movieID in m_ratings_test[userID].Keys)
                {
                    if (iterationNumber >= cTrials)
                        break;
                    double realRating = m_ratings[userID][movieID];

                    if (lMethods.Contains(PredictionMethod.Pearson))
                    {
                        double pearsonRating = PredictRating(PredictionMethod.Pearson, userID, movieID);
                        if (pearsonRating == -1) //invalid user or movie
                            continue;
                        double pearsonError = Math.Pow(realRating - pearsonRating,2);
                        pearsonRMSE += pearsonError;
                    }

                    if (lMethods.Contains(PredictionMethod.Cosine))
                    {
                        double cosineRating = PredictRating(PredictionMethod.Cosine, userID, movieID);
                        if (cosineRating == -1)
                            continue;
                        double cosineError = Math.Pow(realRating - cosineRating,2);
                        cosineRMSE += cosineError;
                    }

                    if (lMethods.Contains(PredictionMethod.Random))
                    {
                        double randomRating = PredictRating(PredictionMethod.Random, userID, movieID);
                        if (randomRating == -1)
                            continue;
                        double randomError = Math.Pow(realRating - randomRating,2);
                        randomRMSE += randomError;
                    }

                    if (lMethods.Contains(PredictionMethod.BaseModel))
                    {
                        double baseRating = PredictRating(PredictionMethod.BaseModel, userID, movieID);
                        if (baseRating == -1)
                            continue;
                        double baseError = Math.Pow(realRating - baseRating, 2);
                        baseModelRMSE += baseError;
                    }
                    if (lMethods.Contains(PredictionMethod.Stereotypes))
                    {
                        double stereoRating = PredictRating(PredictionMethod.Stereotypes, userID, movieID);
                        if (stereoRating == -1)
                            continue;
                        double stereoError = Math.Pow(realRating - stereoRating, 2);
                        stereoTypeRMSE += stereoError;
                    }


                    iterationNumber++;
                }
            
            }
            if (lMethods.Contains(PredictionMethod.Cosine))
                ans.Add(PredictionMethod.Cosine, Math.Sqrt(cosineRMSE / cTrials));
            if (lMethods.Contains(PredictionMethod.Pearson))
                ans.Add(PredictionMethod.Pearson, Math.Sqrt(pearsonRMSE / cTrials));
            if (lMethods.Contains(PredictionMethod.Random))
                ans.Add(PredictionMethod.Random, Math.Sqrt(randomRMSE / cTrials));
            if (lMethods.Contains(PredictionMethod.BaseModel))
                ans.Add(PredictionMethod.BaseModel, Math.Sqrt(baseModelRMSE / cTrials));
            if (lMethods.Contains(PredictionMethod.Stereotypes))
                ans.Add(PredictionMethod.Stereotypes, Math.Sqrt(stereoTypeRMSE / cTrials));
            return ans;
        }

  
    }
}
