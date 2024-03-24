using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
    public sealed class OperationResult<T>
    {
        public bool Success { get; private set; }
        public T? Result { get; private set; }
        public string? ErrorMessage { get; private set; }
        
        public OperationResult(bool isSuccess, T? result, string? errMessage)
        {
            Result = result;
            Success = isSuccess;
            ErrorMessage = errMessage;
        }
    }
}
