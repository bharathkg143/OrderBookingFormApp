using iText.Html2pdf;
using iText.Kernel.Crypto;
using Microsoft.AspNetCore.Mvc;
using MySqlX.XDevAPI;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using iText.Signatures;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using iText.Forms.Form.Element;
using iText.Layout.Borders;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Rotativa.AspNetCore;
using OrderBookingFormApp.Models.ViewModels;
using OrderBookingFormApp.Data;
using OrderBookingFormApp.Models;
using Newtonsoft.Json;
using Microsoft.CodeAnalysis.Elfie.Extensions;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace OrderBookingFormApp.Controllers
{
    public class OrderBookingFormController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderBookingFormController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult OrderBookingForm()
        {
            SalesBookedData? salesBookedData = new SalesBookedData();
            try
            {
                int? customerId = HttpContext.Session.GetInt32("CustomerID");
                int? vehicleId = HttpContext.Session.GetInt32("VehicleID");

                salesBookedData = _context.SalesBookedDatas.Where(x => x.CustomerID == customerId && x.VehicleID == vehicleId).OrderByDescending(x => x.Id).FirstOrDefault();
                return View(salesBookedData);
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

                ViewBag.ErrorGettingData = exception;
                return View(salesBookedData);
            }
        }

        [HttpPost]
        public IActionResult SubmitOrderBookingForm(string custSignature, IFormFile custImageFile, string confirmBookData)
        {
            try
            {
                long? customerId = HttpContext.Session.GetInt32("CustomerID");
                long? vehicleId = HttpContext.Session.GetInt32("VehicleID");

                ConfirmSalesBooking? confirmSalesBooking = JsonConvert.DeserializeObject<ConfirmSalesBooking>(confirmBookData);

                if (!string.IsNullOrEmpty(custSignature) && (custImageFile.Length > 0 && custImageFile != null) && confirmBookData != null)
                {
                    string drivePath = @"C:\PopularSales-OBFDocuments";

                    #region Signature File Saving Section
                    string folderPathForSignatureDocs = Path.Combine(drivePath, "Signatures");
                    var signatureFileName = "sign_" + customerId + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + "_" + $"{Guid.NewGuid().ToString()}.png";

                    var base64Data = custSignature.Replace("data:image/png;base64,", "");
                    var bytes = Convert.FromBase64String(base64Data);

                    if (!Directory.Exists(folderPathForSignatureDocs))
                    {
                        Directory.CreateDirectory(folderPathForSignatureDocs);
                    }

                    var signatureFileFullPath = Path.Combine(folderPathForSignatureDocs, signatureFileName);
                    System.IO.File.WriteAllBytes(signatureFileFullPath, bytes);

                    string customerSignedFilePath = "https://kuttukaran.autosherpas.com/PopularSalesOBFUploads/Signatures/" + signatureFileName;
                    #endregion


                    #region Customer Photo File Saving Section
                    string folderPathForCustomerPhotoDocs = Path.Combine(drivePath, "CustomerPhotos");
                    var customerPhotoFileName = "photo_" + customerId + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + "_" + $"{Guid.NewGuid().ToString()}{Path.GetExtension(custImageFile.FileName)}";

                    if (!Directory.Exists(folderPathForCustomerPhotoDocs))
                    {
                        Directory.CreateDirectory(folderPathForCustomerPhotoDocs);
                    }

                    var customerPhotoFileFullPath = Path.Combine(folderPathForCustomerPhotoDocs, customerPhotoFileName);
                    using (var fileStream = new FileStream(customerPhotoFileFullPath, FileMode.Create))
                    {
                        custImageFile.CopyTo(fileStream);
                    }

                    string customerPhotoFilePath = "https://kuttukaran.autosherpas.com/PopularSalesOBFUploads/CustomerPhotos/" + customerPhotoFileName;
                    #endregion


                    #region PDF Generation And PDF File Saving Section
                    string obfHtmlContent = OBFHtmlContentForPDFGenaration(confirmSalesBooking, customerSignedFilePath, customerPhotoFilePath);

                    string folderPathForObfPdfDocs = Path.Combine(drivePath, "OBForms");
                    var obfPdfFileName = "OBF_" + customerId + "_" + $"{DateTime.Now:yyyyMMddHHmmss}" + "_" + $"{Guid.NewGuid().ToString()}.pdf";

                    var obfPdfFileFullpath = Path.Combine(folderPathForObfPdfDocs, obfPdfFileName);

                    if (!Directory.Exists(folderPathForObfPdfDocs))
                    {
                        Directory.CreateDirectory(folderPathForObfPdfDocs);
                    }

                    using (FileStream fileStream = new FileStream(obfPdfFileFullpath, FileMode.Create))
                    {
                        HtmlConverter.ConvertToPdf(obfHtmlContent, fileStream);
                    }

                    string obfPdfFilePath = "https://kuttukaran.autosherpas.com/PopularSalesOBFUploads/OBForms/" + obfPdfFileName;
                    #endregion


                    #region Remove Old Data If Exists and Saving New to ConfirmSalesBooking Tab
                    List<ConfirmSalesBooking>? removeOldConfirmSalesBookings = _context.ConfirmSalesBookings.Where(x => x.CustomerID == customerId && x.VehicleID == vehicleId).ToList();

                    if (removeOldConfirmSalesBookings != null && removeOldConfirmSalesBookings.Count > 0)
                    {
                        _context.ConfirmSalesBookings.RemoveRange(removeOldConfirmSalesBookings);
                        _context.SaveChanges();
                    }


                    confirmSalesBooking.CustomerID = customerId;
                    confirmSalesBooking.VehicleID = vehicleId;
                    confirmSalesBooking.CustomerSignature_FilePath = customerSignedFilePath;
                    confirmSalesBooking.CustomerPhoto_FilePath = customerPhotoFilePath;
                    confirmSalesBooking.ObfPDF_FilePath = obfPdfFilePath;
                    confirmSalesBooking.ConfirmedDateTime = DateTime.Now;
                    _context.ConfirmSalesBookings.Add(confirmSalesBooking);
                    _context.SaveChanges();
                    #endregion

                    bool isDataInserted = InsertUserDataAfterSubmission(customerId, vehicleId);

                    if (isDataInserted)
                    {
                        ConfirmSalesBooking? confirmSalesBookingIsSubmit = _context.ConfirmSalesBookings.Where(x => x.CustomerID == customerId && x.VehicleID == vehicleId).OrderByDescending(x=> x.Id).FirstOrDefault();
                        if(confirmSalesBookingIsSubmit != null)
                        {
                            confirmSalesBookingIsSubmit.isSubmit = true;
                            _context.ConfirmSalesBookings.Update(confirmSalesBookingIsSubmit);
                            _context.SaveChanges();
                        }
                    }

                    return Json(new { success = true });
                }

                return Json(new { success = false, message = "Please provide mandatory data before submitting form." });
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

                return Json(new { success = false, message = $"An error occurred while saving. {exception}" });
            }
        }

        private bool InsertUserDataAfterSubmission(long? custId, long? vehiId)
        {
            try
            {
                string query = "CALL spInsertLiveData(@customerID, @vehicleID)";
                MySqlParameter[] parameters = new[]
                {
                    new MySqlParameter("@customerID",custId),
                    new MySqlParameter("@vehicleID",vehiId)
                };

                int dataInserted = _context.Database.ExecuteSqlRaw(query, parameters);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private string OBFHtmlContentForPDFGenaration(ConfirmSalesBooking booking, string signature, string customerPhoto)
        {
            //string sign = @"C:\PopularSales-OBFDocuments\Signatures\" + signature;
            //string photo = @"C:\PopularSales-OBFDocuments\CustomerPhotos\" + customerPhoto;

            //string imagePathSign = @"C:\PopularSales-OBFDocuments\Signatures\" + signature;
            //string imagePathPhoto = @"C:\PopularSales-OBFDocuments\CustomerPhotos\" + customerPhoto;
            //string base64ImagePhoto = ImageToBase64(imagePathPhoto);
            //string base64ImageSign = ImageToBase64(imagePathSign);

            //name bind pending, fields value going out

            string htmlContent = $@"
        <html>
        <head>
            <style>
                html {{ position: relative; min-height: 100%; }}
                body {{ margin-bottom: 0px; font-family: 'Roboto', sans-serif;color: #333; }}
                .main-container {{ border: 2px solid black; padding: 10px;margin: 0 auto;width: 100%;max-width: 100vw;box-sizing: border-box;min-height: 100vh; }}
                .form-container {{ margin: 10px; width: 100%; }}
                .form-row {{ display: flex; align-items: center; margin-bottom: 5px; flex-wrap: wrap; }}
                .form-label {{ margin-right: 10px; font-size: 16px; font-weight: bold; flex-basis: 100px; flex-shrink: 0; }}
                .form-control {{ flex: 1; min-height: 38px; max-height: 150px;overflow: auto; word-wrap: break-word;overflow-wrap: break-word; box-sizing: border-box; margin-right: 20px; }}
                .form-control:focus {{ box-shadow: none; }}
                .heading-container {{ margin: 10px; background-color: #e0e0e0; height: 40px; display: flex; align-items: center; justify-content: center; }}
                .heading-container h4 {{ color: black; padding: 5px; margin: 0; }}
                .textarea-auto-resize {{ resize: vertical; }}
                .address-field {{ word-wrap: break-word; white-space: pre-wrap; overflow-wrap: break-word; box-sizing: border-box; }}
            </style>
        </head>

        <body>
<div class=""container-fluid main-container"">
     <main role=""main"" class=""pb-3"">
        <div style='text-align: center;padding-top: 0px;'>
            <h1 style=""color: #007bff; font-weight: bold;""><b>ORDER BOOKING FORM</b></h1>
        </div>

<div class="""" style=""margin-left:10px;padding-top: 0px;"">
    <span style=""font-size: 17px;"">
        Dear Customer, Thank you for booking your next vehicle with POPULAR VEHICLES.<br />
        Please check the below details and CONFIRM your booking.
    </span>
</div>

<div id=""divCustomerDetails"">
    <div class=""mt-3 heading-container shadow-sm"">
        <h4 class=""mt-1"">CUSTOMER DETAILS</h4>
    </div>

    <div class=""form-container mt-3"">
        <div class=""form-row"">
            <label style=""font-size: 12px;"" class=""form-label"">CUSTOMER NAME</label>
             <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value=""{booking.CustomerName}""></input>
        </div>

        <div class=""form-row"">
            <label style=""font-size: 12px;"" class=""form-label"">MOBILE NO.</label>
            <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" type=""text"" value='{booking.MobileNo}' class=""form-control""></input>
            <label style=""font-size: 12px;"" class=""form-label"">OTHER CONTACT NO</label>
            <input style=""border: 1px solid #ced4da; padding: 5px; color: #343a40;"" type=""text"" value='{booking.OtherContactNo}' class=""form-control""></input>
        </div>

        <div class=""form-row"">
            <label style=""font-size: 12px;"" class=""form-label"">E-MAIL</label>
            <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" value='{booking.Email}' class=""form-control""></input>
        </div>

        <div class=""form-row"">
            <label style=""font-size: 12px;"" class=""form-label"">PAN NO.</label>
            <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" value='{booking.PanCardNo}' class=""form-control shadow-sm""></input>
            <label style=""font-size: 12px;"" class=""form-label"">DOB</label>
            <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" value='{booking.DOB}'  class=""form-control shadow-sm""></input>
        </div>

        <div class=""form-row"">
            <label style=""font-size: 12px;"" class=""form-label"">FATHER/<br />HUSBAND NAME</label>
            <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" value='{booking.FatherOrHusbandName}' class=""form-control""></input>
         </div>

        <div class=""form-row"">
            <label style=""font-size: 12px;"" class=""form-label"">ADDRESS</label>
            <textarea style=""height: 55px; width: 100%; overflow: auto;border: 1px solid #ced4da; color: #343a40;font-family: Helvetica, Arial, sans-serif;padding: 5px;"" class=""form-control"">{booking.Address}</textarea>
        </div>

         <div class=""form-row"">
            <label style=""font-size: 12px;"" class=""form-label"">PIN CODE</label>
            <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" value='{booking.PinCode}' class=""form-control""></input>
            <label style=""font-size: 12px;"" class=""form-label"">VILLAGE</label>
            <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" value='{booking.Village}' class=""form-control""></input>
        </div>

        <div class=""form-row"">
            <label style=""font-size: 12px;"" class=""form-label"">CITY</label>
            <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" value='{booking.City}' class=""form-control""></input>
            <label style=""font-size: 12px;"" class=""form-label"">TEHSIL</label>
            <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" value='{booking.Taluk}' class=""form-control""></input>
        </div>


        <div class=""form-row"">
            <label style=""font-size: 12px;"" class=""form-label"">DISTRICT</label>
            <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" value='{booking.District}' class=""form-control""></input>
            <label style=""font-size: 12px;"" class=""form-label"">STATE</label>
            <input style=""border: 1px solid #ced4da;padding: 5px; color: #343a40;"" value='{booking.State}' class=""form-control""></input>
        </div>

     </div>
</div>

<div id=""divBookedVehicleDetails"">
    <div class=""mt-3 heading-container shadow-sm"">
        <h4 class=""mt-1"">BOOKED VEHICLE DETAILS</h4>
    </div>

    <div class=""form-container mt-3"">
        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">DMS OBF NO.</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.DmsObfNo}'>
            <label style="" font-size: 12px;"" class=""form-label"">BOOKING DATE</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.BookingDate}'>
        </div>

        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">BOOKED MODEL</label>
            <textarea style=""height: 40px; width: 100%; border: 1px solid #ced4da;padding: 5px; color: #343a40; word-wrap: break-word;font-family: Helvetica, Arial, sans-serif;"" class=""form-control"">{booking.Model}</textarea>
            <label style="" font-size: 12px;"" class=""form-label"">BOOKED VARIANT</label>
            <textarea style=""height: 40px; width: 100%; border: 1px solid #ced4da;padding: 5px; color: #343a40; word-wrap: break-word;font-family: Helvetica, Arial, sans-serif;"" class=""form-control"">{booking.Variant}</textarea>
        </div>

        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">COLOR</label>
            <textarea style=""height: 40px; width: 100%; border: 1px solid #ced4da;padding: 5px; color: #343a40; word-wrap: break-word;font-family: Helvetica, Arial, sans-serif;"" class=""form-control"">{booking.Color}</textarea>
            <label style="" font-size: 12px;"" class=""form-label"">TENTATIVE WAITING PERIOD</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.TentativeWaitPeriod}'>
        </div>

    </div>
</div>

<div id=""divCostVehicleDetails"">
    <div class=""mt-3 heading-container shadow-sm"">
        <h4 class=""mt-1"">COST OF VEHICLE</h4>
    </div>

    <div class=""form-container mt-3"">
        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">EX- SHOWROOM PRICE</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.ExShowroomPrice}'>
            <label style="" font-size: 12px;"" class=""form-label"">REGISTRATION CHARGES</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.RegistrationCharges}'>
        </div>

        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">TEMP. REG CHARGES</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.TempRegCharges}'>
            <label style="" font-size: 12px;"" class=""form-label"">INSURANCE PRICE</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.InsurancePrice}'>
        </div>

        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">EW (OPTIONAL)</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;""  class=""form-control"" value='{booking.EwOptional}'>
            <label style="" font-size: 12px;"" class=""form-label"">ACCESSORIES</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.Accessories}'>
        </div>

        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">OTHERS (IF ANY)</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.OthersIfAny}'>
            <label style="" font-size: 12px;"" class=""form-label"">OTHERS DESCRIBE</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.OthersDescribe}'>
        </div>

        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">DISCOUNT</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.Discount}'>
            <label style="" font-size: 12px;"" class=""form-label"">CGST @ 14%</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.CGST14Percent}'>
        </div>

        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">SGST @ 14% E</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.SGST14Percent}'>
            <label style="" font-size: 12px;"" class=""form-label"">CESS @ 1%</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.CESS1Percent}'>
        </div>

        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">ON ROAD PRICE</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.OnRoadPrice}'>
            <label style="" font-size: 12px;"" class=""form-label"">BOOKING AMOUNT</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.Amount}'>
        </div>

        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">PAYMENT MODE</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.PaymentMode}'>
            <label style="" font-size: 12px;"" class=""form-label"">PAYMENT REFERENCE</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.PaymentReference}'>
        </div>
    </div>
</div>

<div id=""divOtherDetails"">
    <div class=""mt-3 heading-container shadow-sm"">
        <h4 class=""mt-1"">OTHER DETAILS</h4>
    </div>

    <div class=""form-container mt-3"">
        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">EXCHANGE</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.Exchangestatus}'>
            <label style="" font-size: 12px;"" class=""form-label"">EXISTING CAR MODEL</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.ExistingCarModel}'>
        </div>

        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">REGISTRATION NUMBER</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.RegistrationNo}'>
            <label style="" font-size: 12px;"" class=""form-label"">MSIL LISTED CORPORATE</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.MSILListedCorporate}'>
        </div>

        <div class=""form-row"">
            <label style="" font-size: 12px;"" class=""form-label"">CORPORATE NAME</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.CorporateName}'>
            <label style="" font-size: 12px;"" class=""form-label"">FINANCE AVAILED</label>
            <input style="" border: 1px solid #ced4da;padding: 5px; color: #343a40;"" class=""form-control"" value='{booking.Finance}'>
        </div>
    </div>
</div>

<div style=""margin-left:10px;padding-top: 10px;"">
    <span style=""font-size: 17px;"">
        We hereby give our unconditional consent to be contacted by Maruti Suzuki and /its business associates in relation to the products and services of Maruti Suzuki India Limited by means including but not limited to my telephone /Mobile Phone/E-mail/SMS.<br />
        I hereby give my unconditional consent to MSIL to use itself or disclose the data collected by MSIL with its Parent Company and/ or business associates for the purpose of carrying out the essential business functions like business and market development, building and managing external relationships, ensuring data protecion and seamless provision of services, research and development, technology infrastructure and such other purpose as required by law or regulation. I/We acknowledge that Welcome docket has been provided to me. All Pay Order /Demand Draft should be in favor of [Dealership name] payable at [City Name)
    </span>
</div>

<div style=""margin-left: 10px; margin-right: 10px; padding-top: 20px;"">
    <div style=""width: 60%; display: flex; align-items: center;"">
        <label style=""font-size: 16px; flex-shrink: 0; margin: 0; padding-right: 10px;font-weight: bold;"">NAME</label>
        <input type=""text"" style=""flex-grow: 1; border: 1px solid #ced4da; padding: 5px; margin: 0; color: #343a40;"" class=""form-control"" value='{booking.UserEnteredName}'>
    </div>
</div>


<div style=""display: flex; margin-left: 10px; margin-right: 10px; padding-top: 30px; width: 100%;"">
    <div style=""width: 100%;"">
        <div style=""display: flex; margin: 0;"">
            <div style=""width: 50%; padding-right: 10px;"">
                <label style=""display: block; font-weight: bold;margin-bottom:10px"">SIGNATURE:</label>
                <img style=""border: 2px solid #ADD8E6; box-shadow: 2px 2px 5px rgba(0, 0, 0, 0.1); width: 100%; height: auto;"" src=""{signature}"">
            </div>
            <div style=""width: 50%; padding-left: 10px;"">
                <label style=""display: block; font-weight: bold;margin-bottom:10px;"">PHOTO:</label>
                <img style=""border: 2px solid #ADD8E6; box-shadow: 2px 2px 5px rgba(0, 0, 0, 0.1); width: 100%; height: auto;"" src=""{customerPhoto}"">
            </div>
        </div>
    </div>
</div>

        </main>
    </div>
        </body>
        </html>";

            return htmlContent;
        }

        public string ImageToBase64(string imagePath)
        {
            byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
            return Convert.ToBase64String(imageBytes);
        }

    }
}
