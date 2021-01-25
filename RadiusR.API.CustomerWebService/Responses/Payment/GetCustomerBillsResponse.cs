using RadiusR.DB.Enums;
using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RadiusR.API.CustomerWebService.Responses.Payment
{
    [DataContract]
    public class GetCustomerBillInfo
    {
        [DataMember]
        public decimal SubscriptionCredits { get; set; }
        [DataMember]
        public bool CanHaveQuotaSale { get; set; }
        [DataMember]
        public bool HasUnpaidBills { get; set; }
        [DataMember]
        public bool IsPrePaid { get; set; }
        [DataMember]
        public IEnumerable<GetCustomerBillsResponse> CustomerBills { get; set; }
    }
    [DataContract]
    public class GetCustomerBillsResponse
    {
        [DataMember]
        public long ID { get; set; }
        [DataMember]
        public string ServiceName { get; set; }
        [DataMember]
        public DateTime BillDate { get; set; }
        [DataMember]
        public DateTime LastPaymentDate { get; set; }
        [DataMember]
        public decimal Total { get; set; }
        [DataMember]
        public short Status { get; set; } // billstate enum
        [DataMember]
        public string StatusText { get; set; }
        [DataMember]
        public bool CanBePaid { get; set; }
        [DataMember]
        public bool HasEArchiveBill { get; set; }
        [DataMember]
        public short PaymentTypeID { get; set; }
    }

    [DataContract]
    public partial class CustomerServiceGetCustomerBillsResponse : BaseResponse<GetCustomerBillInfo, SHA1>
    {
        public CustomerServiceGetCustomerBillsResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public GetCustomerBillInfo GetCustomerBillsResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}
