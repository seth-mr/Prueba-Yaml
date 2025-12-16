using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DamasChinas_Server.Common
{
    public class RepositoryValidationException : Exception
    {
        public MessageCode Code { get; }

        public RepositoryValidationException(MessageCode code)
        {
            Code = code;
        }
    }
}

