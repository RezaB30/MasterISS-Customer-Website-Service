using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RadiusR.API.TelekomInfrastructureService
{
    [DataContract]
    public partial class CustomerServiceProvincesRequest : BaseRequest<SHA1> { }
}
