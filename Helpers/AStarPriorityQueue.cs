using System;
using System.Collections.Generic;

namespace FollowBotV2.Helpers
{
    public class AStarPriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private readonly List<(TElement Element, TPriority Priority)> _items = new();

        public int Count => _items.Count;

        public void Enqueue(TElement element, TPriority priority)
        {
            _items.Add((element, priority));
            var i = _items.Count - 1;
            while (i > 0)
            {
                var parent = (i - 1) / 2;
                if (_items[parent].Priority.CompareTo(_items[i].Priority) <= 0)
                    break;
                Swap(i, parent);
                i = parent;
            }
        }

        public TElement Dequeue()
        {
            var result = _items[0].Element;
            _items[0] = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);

            var i = 0;
            while (true)
            {
                var left = 2 * i + 1;
                var right = 2 * i + 2;
                var smallest = i;

                if (left < _items.Count && _items[left].Priority.CompareTo(_items[smallest].Priority) < 0)
                    smallest = left;
                if (right < _items.Count && _items[right].Priority.CompareTo(_items[smallest].Priority) < 0)
                    smallest = right;

                if (smallest == i)
                    break;

                Swap(i, smallest);
                i = smallest;
            }

            return result;
        }

        private void Swap(int i, int j)
        {
            var temp = _items[i];
            _items[i] = _items[j];
            _items[j] = temp;
        }

        public IEnumerable<(TElement Element, TPriority Priority)> UnorderedItems => _items;
    }
}