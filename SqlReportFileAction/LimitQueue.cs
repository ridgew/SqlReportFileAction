using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SqlReportFileAction
{
    public class LimitQueue<T> : IEnumerable<T>
    {
        public LimitQueue(int limit)
        {
            limitLength = limit;
        }

        LinkedList<T> queue = new LinkedList<T>();

        /// <summary>
        /// 入列：当队列大小已满时，把队头的元素移除
        /// </summary>
        /// <param name="item"></param>
        public void Enqueue(T item)
        {
            if (queue.Count >= limitLength)
                queue.RemoveFirst();

            queue.AddLast(item);
        }

        public bool IsFull()
        {
            return queue.Count >= limitLength;
        }

        public T Get(int position)
        {
            if (position >= 0 && position < queue.Count)
            {
                return queue.Skip(position).FirstOrDefault();
            }
            return default(T);
        }

        public T LastOrDefault()
        {
            return queue.LastOrDefault();
        }

        public T FirstOrDefault()
        {
            return queue.FirstOrDefault();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return queue.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return queue.GetEnumerator();
        }

        int limitLength = 0;
        public int Limit { get { return limitLength; } }

        public int Count { get { return queue.Count; } }

        public void Clear()
        {
            queue.Clear();
        }

    }

}
