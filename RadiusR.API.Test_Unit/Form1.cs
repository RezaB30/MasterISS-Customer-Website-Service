using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RadiusR.API.Test_Unit
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void AuthBtn_Click(object sender, EventArgs e)
        {
            var username = PartnerAuthUsernameBox.Text;
            var password = PartnerAuthPasswordBox.Text;
            var request = new GenericServiceSettings();
            PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
            var response = client.Authenticate(new PartnerServiceReference.PartnerServiceAuthenticationRequest()
            {
                AuthenticationParameters = new PartnerServiceReference.AuthenticationRequest()
                {
                    PartnerPasswordHash = HashUtilities.CalculateHash<SHA256>(password),
                    UserEmail = username,
                    SubUserEmail = null
                },
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username
            });
            MessageBox.Show(response.ResponseMessage.ErrorMessage);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
            var request = new GenericServiceSettings();
            var response = client.AddSubUser(new PartnerServiceReference.PartnerServiceAddSubUserRequest()
            {
                AddSubUserRequestParameters = new PartnerServiceReference.AddSubUserRequest()
                {
                    RequestedSubUserEmail = "onr@onr.com",
                    RequestedSubUserName = "onur",
                    RequestedSubUserPassword = "123123",
                    UserEmail = "test@test.com",
                    SubUserEmail = "test@test.com",
                },
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username
            });
            var aaa = response;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
            var request = new GenericServiceSettings();
            var response = client.BillsBySubscriberNo(new PartnerServiceReference.PartnerServiceBillListRequest()
            {
                BillListRequest = new PartnerServiceReference.BillListRequest()
                {
                    SubscriberNo = "2555456163",
                    SubUserEmail = "onr@onr.com",
                    UserEmail = "test@test.com"
                },
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username
            });
            var assd = response;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
            var request = new GenericServiceSettings();
            var response = client.IDCardValidation(new PartnerServiceReference.PartnerServiceIDCardValidationRequest()
            {
                IDCardValidationRequest = new PartnerServiceReference.IDCardValidationRequest()
                {
                    BirthDate = "1995-09-08",
                    FirstName = "ONUR",
                    IDCardType = 2,
                    LastName = "CİVANOĞLU",
                    TCKNo = "55147390880",
                    RegistirationNo = "K13628202"
                },
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username
            });
            var assd = response;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();

            var request = new GenericServiceSettings();
            List<long> bills = new List<long>();
            bills.Add(623347);
            var response = client.PayBills(new PartnerServiceReference.PartnerServicePaymentRequest()
            {
                PaymentRequest = new PartnerServiceReference.PaymentRequest()
                {
                    SubUserEmail = "test@test.com",
                    UserEmail = "test@test.com",
                    BillIDs = bills.ToArray()
                },
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username
            });
            var assd = response;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
            var request = new GenericServiceSettings();
            var response = client.GetCreditReport(new PartnerServiceReference.PartnerServiceCreditReportRequest()
            {
                CreditReportRequest = new PartnerServiceReference.CreditReportRequest()
                {
                    SubUserEmail = "test@test.com",
                    UserEmail = "test@test.com",
                    WithDetails = true
                },
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username
            });
            var assd = response;
        }

        private void button6_Click(object sender, EventArgs e) // register
        {
            PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
            var request = new GenericServiceSettings();
            var response = client.NewCustomerRegister(new PartnerServiceReference.PartnerServiceNewCustomerRegisterRequest()
            {
                Culture = "tr-tr",
                Hash = request.Hash,
                Rand = request.Rand,
                Username = request.Username,
                CustomerRegisterParameters = new PartnerServiceReference.NewCustomerRegisterRequest()
                {
                    CorporateCustomerInfo =null,
                    SubUserEmail = "test@test.com",
                    UserEmail = "test@test.com",
                    IDCardInfo = new PartnerServiceReference.IDCardInfo()
                    {
                        BirthDate = "1997-02-05",
                        CardType = 1,
                        DateOfIssue = "2028-09-04",
                        FirstName = "Muhammed Furkan",
                        LastName = "GÖKBEL",
                        TCKNo = "31135390476",
                        SerialNo = "A13T51176",
                    },
                    CustomerGeneralInfo = new PartnerServiceReference.CustomerGeneralInfo()
                    {
                        BillingAddress = new PartnerServiceReference.AddressInfo()
                        {
                            AddressText = "DEGIRMENCIUSAGI MAH. GÖKOLUK KÜME EVLERI NO: 11 - DAIRE: -        SAIMBEYLI/ADANA",
                            StreetName = "GÖKOLUK KÜME EVLERI",
                            NeighbourhoodName = "FATİH",
                            DistrictName = "SAİMBEYLİ",
                            ProvinceName = "ADANA",
                            AddressNo = 2993529151,
                            Floor = "1",
                            PostalCode = 1,
                            ApartmentNo = "Ic Kapi(Daire) No :null",
                            ApartmentID = 48587750,
                            DoorID = 8781242,
                            StreetID = 932481440,
                            NeighbourhoodID = 385,
                            RuralCode = 445,
                            DistrictID = 1588,
                            ProvinceID = 1,
                            DoorNo = "NO :1 "
                        },
                        ContactPhoneNo = "5556467367",
                        Culture = "en-US",
                        CustomerType = 1,
                        Email = "furkangokbel@gmail.com",
                        OtherPhoneNos = null
                    },
                    SubscriptionInfo = new PartnerServiceReference.SubscriptionRegistrationInfo()
                    {
                        BillingPeriod = 1,
                        ServiceID = 1,
                        SetupAddress = new PartnerServiceReference.AddressInfo()
                        {
                            AddressText = "DEGIRMENCIUSAGI MAH. GÖKOLUK KÜME EVLERI NO: 11 - DAIRE: -        SAIMBEYLI/ADANA",
                            StreetName = "GÖKOLUK KÜME EVLERI",
                            NeighbourhoodName = "FATİH",
                            DistrictName = "SAİMBEYLİ",
                            ProvinceName = "ADANA",
                            AddressNo = 2993529151,
                            Floor = "1",
                            PostalCode = 1,
                            ApartmentNo = "Ic Kapi(Daire) No :null",
                            ApartmentID = 48587750,
                            DoorID = 8781242,
                            StreetID = 932481440,
                            NeighbourhoodID = 385,
                            RuralCode = 445,
                            DistrictID = 1588,
                            ProvinceID = 1,
                            DoorNo = "NO :1 "
                        },
                    },
                    IndividualCustomerInfo = new PartnerServiceReference.IndividualCustomerInfo()
                    {
                        Sex = 1,
                        BirthPlace ="İstanbul",
                        MothersMaidenName = "Taşçı",
                        FathersName = "Şener",
                        MothersName = "Safiye",
                        Nationality = 228,
                        Profession = 141,
                        ResidencyAddress = new PartnerServiceReference.AddressInfo()
                        {
                            AddressText = "DEGIRMENCIUSAGI MAH. GÖKOLUK KÜME EVLERI NO: 11 - DAIRE: -        SAIMBEYLI/ADANA",
                            StreetName = "GÖKOLUK KÜME EVLERI",
                            NeighbourhoodName = "FATİH",
                            DistrictName = "SAİMBEYLİ",
                            ProvinceName = "ADANA",
                            AddressNo = 2993529151,
                            Floor = "1",
                            PostalCode = 1,
                            ApartmentNo = "Ic Kapi(Daire) No :null",
                            ApartmentID = 48587750,
                            DoorID = 8781242,
                            StreetID = 932481440,
                            NeighbourhoodID = 385,
                            RuralCode = 445,
                            DistrictID = 1588,
                            ProvinceID = 1,
                            DoorNo = "NO :1 "
                        }
                    }
                }
            });
            var assd = response;
        }
    }
    public class GenericServiceSettings
    {
        public string Culture { get; set; }
        public string Rand { get; set; }
        public string Username { get; set; }
        private string Password { get; set; }
        public GenericServiceSettings()
        {
            Culture = Thread.CurrentThread.CurrentUICulture.Name;
            Rand = Guid.NewGuid().ToString("N");
            Username = "testwebservice";
            Password = "12345678";
        }
        PartnerServiceReference.PartnerServiceClient client = new PartnerServiceReference.PartnerServiceClient();
        public string Hash { get { return HashUtilities.CalculateHash<SHA256>(Username + Rand + HashUtilities.CalculateHash<SHA256>(Password) + client.GetKeyFragment(Username)); } }
    }
    public static class HashUtilities
    {
        public static string CalculateHash<HAT>(string value) where HAT : HashAlgorithm
        {
            HAT algorithm = (HAT)HashAlgorithm.Create(typeof(HAT).Name);
            var calculatedHash = string.Join(string.Empty, algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("x2")));
            return calculatedHash;
        }
    }
}
