using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
namespace NetObjectToNative
{

    public struct StorageElem
    {
        internal AutoWrap Объект;
        internal int Next;


        internal StorageElem(AutoWrap Объект)
        {

            this.Объект = Объект;
            Next = -1;
        }

        internal StorageElem(AutoWrap Объект, int next)
        {

            this.Объект = Объект;
            Next = next;
        }
    }

    internal class ObjectStorage
    {
        const int НачальноеКоличествоЭлементов = 64;
        StorageElem[] Элементы = new StorageElem[НачальноеКоличествоЭлементов];
        internal int КоличествоЭлементов = 0;
        int РазмерМассива = НачальноеКоличествоЭлементов;
        internal int FirstDeleted = -1;
        static SpinLock sl = new SpinLock();
        public int Add(AutoWrap Объект)
        {



            var элемент = new StorageElem(Объект);

            var gotLock = false;
            try
            {
                sl.Enter(ref gotLock);


                if (FirstDeleted == -1)
                {
                    
                    return AddInArray(элемент);
                }
                else
                {
                    int newPos = FirstDeleted;
                    FirstDeleted = Элементы[newPos].Next;
                    Элементы[newPos] = элемент;
                    return newPos;

                }
            }
            finally
            {
                 if (gotLock) sl.Exit();
            }

        }

        int AddInArray(StorageElem Элемент)
        {

               if (КоличествоЭлементов == РазмерМассива)
                {
                    var temp = new StorageElem[РазмерМассива * 2];
                    Array.Copy(Элементы, 0, temp, 0, Элементы.Length);
                    Элементы = temp;
                    РазмерМассива = Элементы.Length;

                }

                Элементы[КоличествоЭлементов] = Элемент;
                var res = КоличествоЭлементов;
                КоличествоЭлементов++;
                return res;
                   
        }
        public void RemoveKey(int Pos)
        {
            var элементы = Элементы;
            if (Pos > 0 && Pos < элементы.Length && элементы[Pos].Объект != null)
            {
                var gotLock = false;
                try
                {
                    sl.Enter(ref gotLock);

                    var Элемент = new StorageElem(null, FirstDeleted);
                    Элементы[Pos] = Элемент;

                    FirstDeleted = Pos;
                }

                finally
                {
                   if (gotLock) sl.Exit();
                }
            }



        }

        public AutoWrap GetValue(int Pos)
        {
            var элементы = Элементы;
            if (!(Pos > -1 && Pos < элементы.Length))
                return null;

            return элементы[Pos].Объект;

        }

        public int RealObjectCount()
        {

            var res = 0;
            foreach (var элем in Элементы)
                if (элем.Объект != null)
                     res++;

            return res;
        }

    }
}
