using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Unhasher 
{
    /// <summary>
    /// This class provides an generically optimal algorithm for reversing hash algorithms. That is: given the hash, it determines the possible input strings. 
    /// For example given a hashing algorithm: (3 * sum + (a = 1, b =2)) * 5 we can determine that the hash 1205 results from the string 'aaa'.
    /// Furthermore if there are any collisions, the algorithm will find all of them, while still remaining optimal search
    /// </summary>
    /// <typeparam name="TState">A type which represents the hash's internal state during each iteration (can keep track of position or any number of other features)</typeparam>
    internal static class Reverser<TState> where TState:IComparable
    {
        #region Public Methods and Operators

        //Public API for the class
        /// <summary>
        ///     Reverses an iteratively generated hash function
        /// </summary>
        /// <param name="symbols">A string array containing the potential symbols that can be used  (letters of the alphabet etc)</param>
        /// <param name="reversedHashFunction">The hashing function in question, implemented in reverse</param>
        /// <param name="checkFunction">THe hash function reversed minus the last step (which is used for validation purposes)</param>
        /// <param name="acceptanceFunction">Function used to determine if the step is a valid check (the result of the first operation in processing a hash)</param>
        /// <param name="comparer">A comparer function which compares the two states</param>
        /// <param name="terminalState">The final state obtained by hashing</param>
        /// <param name="initialState">The initial state of the hashing algorithm</param>
        /// <param name="maxStringLength">The maximum length cap for the generated strings</param>
        /// <returns>A list of the possible solution strings</returns>
        public static IReadOnlyList<string> ReverseHash(
            IReadOnlyList<string> symbols,
            Func<string, TState, TState> reversedHashFunction,
            Func<string, TState, TState> checkFunction,
            Func<TState, bool> acceptanceFunction,
            TState terminalState,
            TState initialState,
            int maxStringLength)
        {
            //Public API guards
            if (symbols == null)
                throw new ArgumentNullException("Symbol alphabet can not be null");
            if (symbols.Count == 0)
            {
                throw new ArgumentException("Symbol alphabet cannot be empty");
            }
            if (reversedHashFunction == null || checkFunction == null || acceptanceFunction == null)
            {
                throw new ArgumentNullException("Hashing functions cannnot be null");
            }
            if (terminalState.Equals(default(TState)))
            {
                throw new ArgumentNullException("Terminal state cannot be default");
            }
            if (maxStringLength == 0)
            {
                throw new ArgumentOutOfRangeException("Max string length cannot be zero");
            }
            return
                GetSolutionStrings(
                    Solve(
                        symbols,
                        reversedHashFunction,
                        checkFunction,
                        acceptanceFunction,
                        terminalState,
                        initialState,
                        maxStringLength));
        }

        #endregion
        //Inner Node class represents an n-ary tree (only parent nodes are linked as all traversal is handled from leaves)
        //The is class private as to not expose unecessary interfaces
        private class Node<TContent>
        {
            #region Constructors and Destructors

            public Node(TContent initContents)
            {
                _contents = initContents;
                _parentNode = null;
            }

            #endregion

            #region Fields

            private readonly TContent _contents;


            private Node<TContent> _parentNode;

            #endregion

            #region Public Methods and Operators

            public TContent GetContent()
            {
                return _contents;
            }

            private Node<TContent> GetParent()
            {
                return _parentNode;
            }

            //Retrieves the chain of nodes linked the root node, and generates a list of them in reverse order (root is last)
            public IReadOnlyList<Node<TContent>> GetParentChainToRoot()
            {
                var outputList = new List<Node<TContent>>();
                //Default condition
                if (GetParent() == null)
                {
                    return outputList;
                }

                // Separate declaration required for recursive function definition due to generics
                Func<List<Node<TContent>>, Node<TContent>, List<Node<TContent>>> retrieveParentRecursively = null;
                retrieveParentRecursively = (list, node) =>
                {
                    if (!node.HasParent())
                    {
                        list.Add(node);
                        return list;
                    }
                    list.Add(node);
                    return retrieveParentRecursively(list, node.GetParent());
                };
                return retrieveParentRecursively(new List<Node<TContent>>(), this);
            }


            private bool HasParent()
            {
                return _parentNode != null;
            }

            public void SetParent(Node<TContent> initParent)
            {
                _parentNode = initParent;
            }

            #endregion
        }

        #region Methods

        //Main function responsible for finding the string(s) responsible for the input hash
        //Builds a trie representation of the nodes
        private static Node<Tuple<string, TState>>[] Solve(
            IReadOnlyList<string> symbols,
            Func<string, TState, TState> reverseHashFunction,
            Func<string, TState, TState> checkIterationFunction,
            Func<TState, bool> accepts,
            TState terminalState,
            TState initialState,
            int maxStringLength)
        {
            //Create a root for the possible hashes
            var root = new Node<Tuple<string, TState>>(new Tuple<string, TState>(null, terminalState));
            //A list of lists of nodes, instantiates second set as empty to set up initial conditions
            var listsOfNodes =
                new List<Node<Tuple<string, TState>>>[1].Select(list => new List<Node<Tuple<string, TState>>>()).ToList();
            //Instatiates the first iteration as the root node
            listsOfNodes[0].Add(root);
            var leafNodes = new List<Node<Tuple<string, TState>>>();
            Func<TState, TState, bool> validationFunc = (a, b) => a.CompareTo(b) >= 0;
            //Iterates over the inputs and builds the trie so long as there is a node with a positive sum value
            for (var answerIndex = 0;
                //Check for any nodes in the last iteration
                listsOfNodes[answerIndex].Any()
                //Check if all the nodes in the current iteration are lead nodes
                && listsOfNodes[answerIndex].Aggregate(true, (aggregate, node) => validationFunc(node.GetContent().Item2 ,initialState) && aggregate)
                //
                && answerIndex < maxStringLength;
                answerIndex++)
            {
                //Getting the list of nodes for the current iteration
                var currentlistOfNodes = listsOfNodes[answerIndex];
                //Instantiating the next list
                listsOfNodes.Add(new List<Node<Tuple<string, TState>>>());
                Parallel.ForEach(
                    currentlistOfNodes,
                    currentNode =>
                    {
                        //Check all possible symbols in the symbol array in parallel
                        foreach (var newNode in from t in symbols.AsParallel()
                            where
                            //Checking if the possible string (with the tacked on symbol) is accepted
                                accepts(
                                    checkIterationFunction(
                                        t,
                                        currentNode.GetContent().Item2)) &&
                                        checkIterationFunction(t, currentNode.GetContent().Item2).CompareTo(initialState) >= 0
                            //Transforms the selected symbol into a new node with the modified state          
                            select
                                new Node<Tuple<string, TState>>(
                                    new Tuple<string, TState>(
                                        t,
                                        reverseHashFunction(
                                            t,
                                            currentNode.GetContent().Item2))))
                        {   
                            //Adds the node to the current iteration
                            listsOfNodes[answerIndex + 1].Add(newNode);
                            //Setting the relevant parents and children
                            newNode.SetParent(currentNode);
                            //Check if the node is a leaf by comparing it to the initial state (i.e. the seed value for the hashing algorithm)
                            if (newNode.GetContent().Item2.Equals(initialState))
                            {
                                leafNodes.Add(newNode);
                            }
                        }
                    });
            }
            return leafNodes.ToArray();
        }

        //Given a leaf node of the trie the function builds a string by recursively building a list to the parent node
        private static string BuildSolutionStringFromNode(Node<Tuple<string, TState>> leafNode)
        {
            return leafNode.GetParentChainToRoot().Aggregate(string.Empty, (s, node) => node.GetContent().Item1 + s);
        }


        //Builds out all the strings possibly generated from an array of leaf nodes
        private static IReadOnlyList<string> GetSolutionStrings(Node<Tuple<string, TState>>[] leafNodes)
        {
            return leafNodes.Select(BuildSolutionStringFromNode).ToList();
        }

        #endregion
    }

    //Test Class 
    public class Test {
        public static void Main()
        {
            var inputString = "aaa";
            var hashResult = inputString.Aggregate(0, (i, c) => (i*3 + (c.Equals('a') ? 1 : 2))*5);//Hash('aaa') = 1205
            Console.WriteLine("Hash of aaa = " + hashResult);
            //Simple test
            List<string> symbolLibrary = new List<string> { "a", "b" };
            Func<string, long, long> reversedHashFunc = (str, ste) => ((ste/5) - (str.Equals("a") ? 1 : 2))/3;
            Func<string, long, long> checkIterationFunc = (str, ste) => (ste/5) - (str.Equals("a") ? 1 : 2);
            Func<long, bool> acceptanceFunc = (ste) => ste % 3 == 0;
            long terminalValue = hashResult;
            long initialValue = 0;
            int maxStringlength = 3;
            var answers = Reverser<long>.ReverseHash(symbolLibrary, reversedHashFunc, checkIterationFunc, acceptanceFunc,
                terminalValue, initialValue, maxStringlength);
            Console.WriteLine("The possible input strings for the hash " + hashResult + " are: ");
            foreach (var answer in answers)
            {
                Console.WriteLine(answer);
            }
            Console.ReadLine();
        } }
}
