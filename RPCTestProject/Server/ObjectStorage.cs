namespace Server
{
    using NetObjectToNative;
    using System;
    using System.Threading;

    /// <summary>
    /// Хранилище элементов
    /// </summary>
    internal class ObjectStorage
    {
        public ObjectStorage()
        {
            _elements = new StorageElement[DefaultCountElements];
            _arraySize = DefaultCountElements;
            _firstDeleted = -1;
            _sLock = new SpinLock();
        }

        internal int Add(AutoWrap @object)
        {
            var element = new StorageElement(@object);
            var gotLock = false;
            try
            {
                _sLock.Enter(ref gotLock);

                if (_firstDeleted == -1) return AddInArray(element);
                else
                {
                    int newPosition = _firstDeleted;
                    _firstDeleted = _elements[newPosition].Next;
                    _elements[newPosition] = element;
                    return newPosition;
                }
            }
            finally
            {
                if (gotLock) _sLock.Exit();
            }
        }

        internal void RemoveKey(int position)
        {
            if (position > 0 && position < _elements.Length && _elements[position].Object != null)
            {
                var gotLock = false;
                try
                {
                    _sLock.Enter(ref gotLock);

                    var element = new StorageElement(null, _firstDeleted);
                    _elements[position] = element;
                    _firstDeleted = position;
                }
                finally
                {
                    if (gotLock) _sLock.Exit();
                }
            }
        }

        internal AutoWrap GetValue(int position)
        {
            if (!(position > -1 && position < _elements.Length)) return null;
            return _elements[position].Object;
        }

        internal int RealObjectCount()
        {
            var count = 0;
            foreach (var element in _elements)
                if (element.Object != null) count++;
            return count;
        }

        private int AddInArray(StorageElement element)
        {
            if (_elementsCount == _arraySize)
            {
                _arraySize = _arraySize * 2;
                var temp = new StorageElement[_arraySize];
                Array.Copy(_elements, 0, temp, 0, _elements.Length);
                _elements = temp;
            }

            _elements[_elementsCount] = element;
            //todo проверить количество элементов (сначала нужно вернуть начальное значение, потом добавить + 1)
            return _elementsCount++;
        }

        private const int DefaultCountElements = 64;
        private StorageElement[] _elements;
        private int _elementsCount;
        private int _arraySize;
        private int _firstDeleted;
        private static SpinLock _sLock;
    }

    /// <summary>
    /// Элемент в хранилище объектов
    /// </summary>
    internal struct StorageElement
    {
        internal AutoWrap Object;
        internal int Next;

        public StorageElement(AutoWrap @object)
        {
            Object = @object;
            Next = -1;
        }

        public StorageElement(AutoWrap @object, int next)
        {
            Object = @object;
            Next = next;
        }
    }
}