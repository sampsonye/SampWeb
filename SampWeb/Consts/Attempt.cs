using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampWeb.Consts
{
    public class Attempt<T> where T:class 
    {
        public bool IsError { get; set; }
        public T Result { get; set; }
        public string Msg { get; set; }

        public static Attempt<T> CreateAttempt<T>(bool isError, T data, string message) where T : class 
        {
            return new Attempt<T>()
            {
                IsError = isError,
                Result = data,
                Msg = message
            };
        }
    }
}
