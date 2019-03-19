
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MapRoutes
{
    class Program
    {

        public class Route {
            [JsonProperty(PropertyName = "path")]
            public string Path { get; set; }

            [JsonProperty(PropertyName = "pathMatch")]
            public string PathMatch { get; set; }
        }

        static List<string> filterList = new List<string>(new string[]{
                                            "PageNotFound", 
                                            "page-not-found",
                                            "**",
                                        });

        static void Main(string[] args)
        {

            if (args.Length < 1) {
                Console.WriteLine("[ERROR] the project path is required");
                Environment.Exit(1);
            } 

            string root = args[0];
            
            List<Route> fe_routes = ParseProject(root);

            foreach (Route r in fe_routes) {
                Console.WriteLine($"[DEBUG] path found --> {r.Path}");
            }

            WriteCacheFile(fe_routes, root);

            Console.WriteLine("[INFO] \x1b[32mcomplete!\x1b[m");

        }

        public static List<Route> ParseProject(string root) {

            string rootBase = root.Split('\\')[root.Split('\\').Length - 1];
            List<Route> routes = new List<Route>();

            // Populate list with routes
            ParseModules(root, rootBase, routes);

            // Filter out unneeded routes
            routes.RemoveAll(ele => filterList.Contains(ele.Path));

            // Convert routes to regex
            routes.ForEach(ele => AngularRouteToCSharp(ele));

            return routes;
        }

        public static void AngularRouteToCSharp(Route r) {

           string[] endPoint = r.Path.Split("/");

           for (int i = 0; i < endPoint.Length; i++) {
               if (endPoint[i].StartsWith(":")) {

                   // Angular routing variable to C# Regex for easier pattern matching
                   endPoint[i] = @"[a-zA-z1-9\_]+$";
               }
           }

           r.Path = string.Join("/", endPoint);
        }
        public static List<int> AllIndexesOf(string str, string value) {
            if (String.IsNullOrEmpty(value)) {
                throw new ArgumentException("the string to find may not be empty", "value");
            }

            List<int> indexes = new List<int>();
            for (int index = 0;; index += value.Length) {
                index = str.IndexOf(value, index);
                if (index == -1) {
                    return indexes;
                }

                indexes.Add(index);
            }
        }

        // static void HandleDeserializationError(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs errorArgs) {
        //     var currentError = errorArgs.ErrorContext.Error.Message;
        //     errorArgs.ErrorContext.Handled = true;
        //     Console.WriteLine($"[DEBUG] Skipping component: {currentError}");
        // }

        static void ParseRoutingFile(string file, List<Route> routes) {

            if (file.Contains("node_modules")) {
                return;
            }

            string fileString = String.Join("\n", File.ReadAllLines(file));
            /* Find `: Routes` */

            int offset = fileString.IndexOf(": Routes");

            if (offset == -1) {
                Console.Write($"[WARNING] `Routes` type annotation could not be found for {file}");
                return;
            }

            /* Move to first "[" */

            StringBuilder fakeJsonStringB = new StringBuilder();

            for (int i = offset; i < fileString.Length; i++) {
                if (fileString[i] == '[') {
                    fakeJsonStringB.Append('[');
                    offset = i;
                    break;
                }
            }

            /* for every "[" increment a counter and decrement for "]" and break when all brackets are closed */

            char curChar;
            int counter = 1; // We already have one bracket opened

            for (int i = offset+1; i < fileString.Length; i++) {

                curChar = fileString[i];

                // Console.WriteLine($"[DEBUG] counter: {counter}, current Char {curChar}");
                fakeJsonStringB.Append(curChar);

                // Trivial way to see how many brackets we have open 
                if (curChar == '[') {
                    counter++;
                } else if (curChar == ']') {
                    counter--;
                }

                // Are all brackets closed
                if (counter == 0) {
                    break;
                }
                
            }

            string fakeJsonString = fakeJsonStringB.ToString().Trim();
            // Console.WriteLine($"[DEBUG] {jsonString}");

            /*  Convert JSON string to List  */

            /* Hopefully C# has something like javascript jsonify whereby some attributes can be ignored !! Easy way !! */

            /* JAVASCRIPT IS AWFUL AND TERRIBLE */
            // List<Route> jsonArray;
            // try {
            //     jsonArray = JsonConvert.DeserializeObject<List<Route>>(jsonString, new JsonSerializerSettings {
            //         Error = HandleDeserializationError
            //     });
            // } catch (Exception e) {
            //     Console.WriteLine(e);
            //     Console.WriteLine($"[INFO] routes could not be parsed in {file} which looks like \n{jsonString}");
            //     return;
            // }

            // if (jsonArray is null) {
            //     Console.WriteLine($"[WARNING] jsonArray is null for {file}");
            //     Console.WriteLine($"[INFO] failed json looks like so: {jsonString}");
            //     return;
            // }
            
            // foreach (Route result in jsonArray) {
            //     routes.Add(result);
            // }


            /* If that isn't available then things like have to be done manually (i.e. Find path and fullPath attributes and string parse the values out) !! Hard way !! */

            // Get list of indicies where `pathMatch` is in the routing file
            List<int> pathMatchIndicies = AllIndexesOf(fakeJsonString, "pathMatch");

            // Get the list of indicies where `path` is but not `pathMatch`
            List<int> indicies = AllIndexesOf(fakeJsonString, "path").Except(pathMatchIndicies).ToList();

            int curIndex;
            Route tempRoute;
            StringBuilder pathValue = new StringBuilder();
            foreach (int index in indicies) {

                // Start pointer at the index for `path`
                curIndex = index;

                // Clear out the buffer
                pathValue.Clear();

                // Move to the start of the value
                while (fakeJsonString[curIndex] != ':') {
                    curIndex++;
                }

                curIndex++;

                // Grab the rest of the `path` Value
                while (fakeJsonString[curIndex] != ',' && fakeJsonString[curIndex] != '}' && curIndex < fakeJsonString.Length) {
                    pathValue.Append(fakeJsonString[curIndex]);
                    curIndex++;
                }

                // Clean things up before making it a route
                if (pathValue.ToString().Trim(new char[]{' ', '\''}) == "") {
                    continue;
                }

                string matchPathValue = "false";

                foreach (int matchIndex in pathMatchIndicies) {
                    if (matchIndex < index) {
                        if (fakeJsonString.Substring(matchIndex, index - matchIndex).IndexOf('}') == -1) {
                            // `path` and `pathMatch` are in the same object
                            matchPathValue = "true";
                        }
                    } else {
                        if (fakeJsonString.Substring(index, matchIndex - index).IndexOf('}') == -1) {
                            // `path` and `pathMatch` are in the same object
                            matchPathValue = "true";
                        }
                    }
                }

                // Make a new route and add it to the list of routes
                tempRoute = new Route();
                tempRoute.Path = pathValue.ToString().Trim(new char[]{' ', '\''});
                tempRoute.PathMatch = matchPathValue;
                routes.Add(tempRoute);
            }
        }

        static void ParseModules(string parent, string basename, List<Route> routes) {
            // Grab all of the routing files

            IEnumerable<string> files = null;
            try {
                files = Directory.EnumerateFiles(parent, "*-routing.module.ts", SearchOption.AllDirectories);
            } catch (DirectoryNotFoundException) {
                Console.WriteLine($"[ERROR] The directory `{parent}` is not found");
                Environment.Exit(1);
            }

            Console.WriteLine($"[INFO] \x1b[95m{ files.Count().ToString() } routing files found\x1b[m");

            // Parse the routing files (Concurrently in this case)
            Parallel.ForEach (files, (f) => {

                if (f.Contains("node_modules")) {
                    return;
                }

               Console.WriteLine($"[DEBUG] parsing {f}");


               ParseRoutingFile(f, routes);
            });

            Console.WriteLine("[INFO] \x1b[32mparsing complete\x1b[m");
 
        }

        static void WriteCacheFile(List<Route> routes, string root) {
            using (StreamWriter file = new StreamWriter($"{root}\\paths.json")) {

                string output = JsonConvert.SerializeObject(routes, Formatting.Indented);

                file.WriteLine(output);

            }
        }

    }
}
