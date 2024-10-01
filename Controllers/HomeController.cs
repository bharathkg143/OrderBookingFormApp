using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using OrderBookingFormApp.Data;
using OrderBookingFormApp.Models;
using OrderBookingFormApp.Models.ViewModels;
using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using static System.Net.WebRequestMethods;

namespace OrderBookingFormApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [HttpGet]
        public IActionResult Index(string? id, string? submit)
        {
          //try JWT
            if (!string.IsNullOrEmpty(submit) && submit == "1")
            {
                return View();
            }
            try
            {

                id = "699751,621428";
                if (!string.IsNullOrEmpty(id))
                {
                    string[] userIds = id.Split(',');

                    long customerId = long.Parse(userIds[0]);
                    long vehicleId = long.Parse(userIds[1]);

                    if (customerId != 0 && vehicleId != 0)
                    {
                        string sbStatus = SaveSalesBookedData(customerId, vehicleId);

                        if (sbStatus == "Saved")
                        {
                            HttpContext.Session.SetInt32("CustomerID", Convert.ToInt32(customerId));
                            HttpContext.Session.SetInt32("VehicleID", Convert.ToInt32(vehicleId));
                            return View();
                        }
                        ViewBag.Status = "Error";
                        ViewBag.Message = sbStatus;
                        return View();
                    }
                    ViewBag.Status = "Error";
                    ViewBag.Message = "Incorrect URL. Please check the URL and try again.";
                    return View();
                }
                ViewBag.Status = "Error";
                ViewBag.Message = "Incorrect URL. Please check the URL and try again.";
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Status = "Error";
                ViewBag.Message = $"Error: {ex.Message}";
                return View();
            }
        }

        [HttpPost]
        public IActionResult PhoneNumber(string phnNumber)
        {
            try
            {
                int? customerId = HttpContext.Session.GetInt32("CustomerID");
                int? vehicleId = HttpContext.Session.GetInt32("VehicleID");

                string mobileNumber = phnNumber.Trim();
                if ((!mobileNumber.StartsWith("+91") && mobileNumber.Length == 10) || (mobileNumber.StartsWith("+91") && mobileNumber.Length == 13))
                {

                    int otpToSend = GenarateOTP();

                    //api implement pending for send otp
                    SendOtpSms(mobileNumber, otpToSend);

                    //if(SendOtpSms is true)
                    //{
                    //    //OTP saving part
                    //    GeneratedOTP otpDetails = new GeneratedOTP();
                    //    if (_context.GeneratedOTPs.Count(m => m.CustomerId == customerId) > 0)
                    //    {
                    //        var removeOtpDetails = _context.GeneratedOTPs.FirstOrDefault(m => m.CustomerId == customerId);
                    //        _context.GeneratedOTPs.Remove(removeOtpDetails);
                    //        _context.SaveChanges();
                    //    }

                    //    otpDetails.CustomerId = customerId;
                    //    otpDetails.OTP = otpToSend;
                    //    _context.GeneratedOTPs.Add(otpDetails);
                    //    _context.SaveChanges();
                    //}

                    dynamic numbers = new
                    {
                        normalNumber = mobileNumber,
                        maskedNumber = MaskedNumber(mobileNumber)
                    };

                    TempData["MaskedPhoneNumber"] = numbers.maskedNumber;
                    TempData["Success"] = "OTP Sent to your Registered Mobile Number Successfull..";
                    return Json(new { success = true });
                }
                return Json(new { success = false, message = "Entered Mobile Number is not valid..!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        public IActionResult EnterOTP()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ValidateOTP(string otp)
        {
            //try
            //{
            int? customerId = HttpContext.Session.GetInt32("CustomerID");
            int? vehicleId = HttpContext.Session.GetInt32("VehicleID");
            //    long? genereateOTP = _context.GeneratedOTPs.Where(x => x.CustomerId == customerId).Select(x => x.OTP).FirstOrDefault();
            //    if (genereateOTP != null)
            //    {
            //        if (!string.IsNullOrEmpty(otp))
            //        {
            //            if (genereateOTP.ToString() == otp)
            //            {
            return Json(new { success = true });
            //            }
            //            else
            //            {
            //                return Json(new { success = false, message = "Entered OTP is not valid. Please try again.." });
            //            }
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    return Json(new { success = false, message = $"Error: {ex.message}" });
            //}
            //return View();
        }

        private string SaveSalesBookedData(long customerId, long vehicleId)
        {
            try
            {
                SalesBookedData? removeOldSalesBookedData = _context.SalesBookedDatas.Where(x => x.CustomerID == customerId && x.VehicleID == vehicleId).FirstOrDefault();
                if (removeOldSalesBookedData != null)
                {
                    _context.SalesBookedDatas.Remove(removeOldSalesBookedData);
                    _context.SaveChanges();
                }

                string query = "CALL spGetSalesBookedData(@customerId, @vehicleId)";
                MySqlParameter[] parameters = new[] {
                            new MySqlParameter("@customerId",customerId),
                            new MySqlParameter("@vehicleId",vehicleId)
                };

                List<SalesBookedDataVM>? sbDataList = _context.Database.SqlQueryRaw<SalesBookedDataVM>(query, parameters).ToList();

                if (sbDataList != null && sbDataList.Count > 0)
                {
                    SalesBookedDataVM? sbData = sbDataList.LastOrDefault();

                    if (sbData != null)
                    {
                        SalesBookedData salesBookedData = new SalesBookedData
                        {
                            CustomerID = customerId,
                            VehicleID = vehicleId,
                            CustomerName = sbData.customerName,
                            MobileNo = sbData.mobileNo,
                            PhoneNumbers = sbData.phoneNumbers,
                            OtherContactNo = sbData.otherContactNo,
                            Email = sbData.email,
                            PanCardNo = sbData.panNo,
                            FatherOrHusbandName = sbData.fatherOrHusbandName,
                            DOB = Convert.ToDateTime(sbData.dob).Date.ToString("dd-MM-yyyy"),
                            DOA = Convert.ToDateTime(sbData.doa).Date.ToString("dd-MM-yyyy"),
                            Age = sbData.age,
                            Address = sbData.address,
                            PinCode = sbData.pincode,
                            Village = sbData.village,
                            City = sbData.city,
                            Taluk = sbData.thaluk,
                            District = sbData.district,
                            State = sbData.state,
                            DmsObfNo = sbData.dmsObfNo,
                            BookingDate = Convert.ToDateTime(sbData.bookingdate).Date.ToString("dd-MM-yyyy"),
                            Model = sbData.model,
                            Variant = sbData.variant,
                            Color = sbData.color,
                            TentativeWaitPeriod = sbData.tentativeWaitPeriod,
                            ExShowroomPrice = sbData.exShowroomPrice,
                            RegistrationCharges = sbData.registrationCharges,
                            InsurancePrice = sbData.insurancePrice,
                            TempRegCharges = sbData.tempRegCharges,
                            EwOptional = sbData.ewOptional,
                            Accessories = sbData.accessories,
                            OthersIfAny = sbData.othersIfAny,
                            OthersDescribe = sbData.othersDescribe,
                            Discount = sbData.discount,
                            CGST14Percent = sbData.cgst14Percent,
                            SGST14Percent = sbData.sgst14Percent,
                            CESS1Percent = sbData.cess1Percent,
                            OnRoadPrice = sbData.onRoadPrice,
                            Amount = sbData.amount,
                            PaymentMode = sbData.paymentMode,
                            PaymentReference = sbData.paymentReference,
                            Exchangestatus = sbData.exchangestatus,
                            MSILListedCorporate = sbData.msilListedCorporate,
                            CorporateName = sbData.corporateName,
                            Finance = sbData.finance,
                            ExistingCarModel = sbData.existingCarModel,
                            RegistrationNo = sbData.registrationNo,
                            Expecteddate = Convert.ToDateTime(sbData.expecteddate).Date.ToString("dd-MM-yyyy"),
                            Promiseddate = Convert.ToDateTime(sbData.promiseddate).Date.ToString("dd-MM-yyyy"),
                            CustomerType = sbData.customertype,
                            Profession = sbData.profession,
                            SourceOfEnquiry = sbData.sourcetype,
                            SourceDetails = sbData.sourcetypedetails,
                        };
                        _context.SalesBookedDatas.Add(salesBookedData);
                        _context.SaveChanges();

                        return "Saved";
                    }
                }
                return "User Booking Data Not Found in Server..";
            }
            catch (Exception ex)
            {
                string exception = string.Empty;
                if (ex.InnerException != null)
                {
                    if (ex.InnerException.InnerException != null)
                    {
                        exception = ex.InnerException.InnerException.Message;
                    }
                    exception = ex.InnerException.Message;
                }
                else
                {
                    exception = ex.Message;
                }
                return $"Error While getting data: {ex.Message}";
            }
        }

        private void SendOtpSms(string mobileNumber, int otp)
        {

        }

        private int GenarateOTP()
        {
            var random = new Random();
            return random.Next(1000, 9999);
        }

        private string MaskedNumber(string phoneNumber)
        {
            if (!string.IsNullOrEmpty(phoneNumber))
            {
                if (phoneNumber.Length >= 10 && !phoneNumber.StartsWith("+91"))
                {
                    return phoneNumber.Substring(0, 2) + "XXXX" + phoneNumber.Substring(6);
                }
                else if (phoneNumber.Length >= 13 && phoneNumber.StartsWith("+91"))
                {
                    return phoneNumber.Substring(0, 5) + "XXXX" + phoneNumber.Substring(9);
                }
                return phoneNumber;
            }
            return phoneNumber;
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
