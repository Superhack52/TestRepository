namespace NetObjectToNative
{
    using System;
    using System.Linq;
    using System.Threading;

    public struct StorageElem
    {
        internal AutoWrap Wrap;
        internal int Next;

        internal StorageElem(AutoWrap wrap)
        {
            Wrap = wrap;
            Next = -1;
        }

        internal StorageElem(AutoWrap wrap, int next)
        {
            Wrap = wrap;
            Next = next;
        }
    }

    internal class ObjectStorage
    {
        private const int DefaultCountObjects = 64;
        private StorageElem[] _elements = new StorageElem[DefaultCountObjects];
        internal int ElementsCount = 0;
        private int _arraySize = DefaultCountObjects;
        internal int FirstDeleted = -1;
        private static SpinLock _lockObject = new SpinLock();

        public int Add(AutoWrap obj)
        {
            var element = new StorageElem(obj);
            var gotLock = false;
            try
            {
                _lockObject.Enter(ref gotLock);

                if (FirstDeleted == -1) return AddInArray(element);
                else
                {
                    int newPos = FirstDeleted;
                    FirstDeleted = _elements[newPos].Next;
                    _elements[newPos] = element;
                    return newPos;
                }
            }
            finally
            {
                if (gotLock) _lockObject.Exit();
            }
        }

        private int AddInArray(StorageElem element)
        {
            if (ElementsCount == _arraySize)
            {
                var temp = new StorageElem[_arraySize * 2];
                Array.Copy(_elements, 0, temp, 0, _elements.Length);
                _elements = temp;
                _arraySize = _elements.Length;
            }

            _elements[ElementsCount] = element;
            var res = ElementsCount;
            ElementsCount++;
            return res;
        }

        public void RemoveKey(int position)
        {
            if (position > 0 && position < _elements.Length && _elements[position].Wrap != null)
            {
                var gotLock = false;
                try
                {
                    _lockObject.Enter(ref gotLock);

                    var element = new StorageElem(null, FirstDeleted);
                    _elements[position] = element;
                    FirstDeleted = position;
                }
                finally
                {
                    if (gotLock) _lockObject.Exit();
                }
            }
        }

        public AutoWrap GetValue(int position)
        {
            if (!(position > -1 && position < _elements.Length)) return null;
            return _elements[position].Wrap;
        }

        public int RealObjectCount()
        {
            return _elements.Count(element => element.Wrap != null);
        }
    }
}