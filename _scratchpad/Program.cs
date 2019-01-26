using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _scratchpad
{
    class Program
    {
        static void Main(string[] args)
        {
            //ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
            //Task.Factory.StartNew(() => badMethod(queue));

            BlockingCollection<string> queue = new BlockingCollection<string>(boundedCapacity:10);
            Task.Factory.StartNew(() => goodMethod(queue));

            do
            {
                Console.WriteLine("Enter Something:");
                var input = Console.ReadLine();
                if (input == "q")
                    break;

                queue.TryAdd(input, 0);
            } while (true);
        }

        private static void goodMethod(BlockingCollection<string> queue)
        {
            while (true)
            {
                string message = queue.Take();
                Console.WriteLine($"\tDequeued Message:\n\t{message}");
            }
        }

        public static void badMethod(ConcurrentQueue<string> queue)
        {
            while (true)
            {
                string message;
                while (!queue.TryDequeue(out message))
                    continue;

                Console.WriteLine($"\tDequeued Message:\n\t{message}");
            }
        }
    }
}
