using System;
using System.Collections.Generic;
using System.Threading;

namespace Periscope
{
    public class LRUCache<TKey, TValue>
    {
        class Node
        {
            public TKey key;
            public TValue value;
            public Node prev;
            public Node next;
            public DateTime time;

            public Node(TKey key, TValue value)
            {
                this.key = key;
                this.value = value;
                prev = null;
                next = null;
                time = DateTime.UtcNow;
            }

            public void Touch()
            {
                time = DateTime.UtcNow;
            }
        }

        public delegate void ProcessRemovedItem(TValue item);

        readonly int capacity;
        readonly Dictionary<TKey, Node> items;
        readonly int ttl;
        readonly ProcessRemovedItem processRemovedItemFunc;
        Node head;
        Node tail;
        Timer timer;

        public LRUCache(int capacity = 50000, int ttlInMilliseconds = 0, ProcessRemovedItem func = null)
        {
            this.capacity = capacity;
            items = new Dictionary<TKey, Node>(this.capacity);
            ttl = ttlInMilliseconds;
            processRemovedItemFunc = func;
            head = null;
            tail = null;

            if (ttl > 0)
            {
                timer = new Timer(RemoveStaleItems, null, ttl, 10000);
            }
        }

        public int Count
        {
            get {
                return items.Count;
            }
        }

        public bool ContainsKey(TKey key)
        {
            return items.ContainsKey(key);
        }

        public void Add(TKey key, TValue value)
        {
            lock (this)
            {
                Node node = null;
                if (items.TryGetValue(key, out node))
                {
                    node.value = value;
                    node.Touch();
                }
                else
                {
                    if (Count == capacity)
                    {
                        // no more room - vacate first
                        Node tail = this.tail;
                        Pop(tail);
                        processRemovedItemFunc(tail.value);
                    }
                    node = new Node(key, value);
                }

                Push(node);

                if (this.tail == null)
                {
                    tail = head;
                }
            }
        }
        		
        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default(TValue);

            Node node;
            if (!this.items.TryGetValue(key, out node))
            {
                return false;
            }

            lock (this)
            {
                Push(node);
                value = node.value;
            }

            return true;
        }

		public bool Remove(TKey key)
		{
			var ret = false;
			lock (this)
			{
				Node node = null;
				if (this.items.TryGetValue(key, out node))
				{
					this.Pop(node);
					ret = true;
				}
			}
			return ret;
		}

		void Pop(Node node)
		{
			Node prev = node.prev;
			Node next = node.next;
			if (next != null)
			{
				next.prev = prev;
			}
			if (prev != null)
			{
				prev.next = next;
			}
			if (head == node)
			{
				head = next;
			}
			if (tail == node)
			{
				tail = prev;
			}
			items.Remove(node.key);
		}

		void Push(Node node)
		{
			if (node != head)
			{
				if (items.ContainsKey(node.key))
				{
					Pop(node);
				}

				node.prev = null;
				node.next = head;

				if (head != null)
				{
					head.prev = node;
				}

				head = node;

				items.Add(node.key, node);
			}
		}

		void RemoveStaleItems(object state)
        {
            if (ttl == 0)
            {
                return;
            }

            lock (this)
            {
                Node itemToRemove = tail;
                DateTime time = DateTime.UtcNow;

                var timeDiff = (time - itemToRemove.time).TotalMilliseconds;
                while (itemToRemove != null && timeDiff > ttl)
                {
                    Pop(itemToRemove);
                    processRemovedItemFunc(itemToRemove.value);
                    itemToRemove = itemToRemove.prev;
                    timeDiff = (time - itemToRemove.time).TotalMilliseconds;
                }
            }
        }
    }
}