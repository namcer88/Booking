﻿using Booking.Common;
using Booking.DacLayer;
using Booking.Models;
using Booking.Utilities;
using Booking.Utilities.Attributes;
using Booking.Utilities.Base;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Web.Security;

namespace Booking.Controllers
{
    public class AccountController : CustomController
    {
        public JavaScriptSerializer serializer = new JavaScriptSerializer();
        public AccountDac account = new AccountDac();
        public ActionResult Login(string ReturnUrl)
        {
            ViewBag.ReturnUrl = ReturnUrl;
            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }
        [HttpPost]
        public ActionResult Login(string loginName, string password,string returnUrl)
        {
            if (Request.IsAjaxRequest())
            {
                string pwd = MD5Hash.CreateMD5Hash(password); 
                User u = account.CheckLogin(loginName, pwd);
                if (u.BaseResult.Code == 0)
                {
                    CreateAuthenticationTicket(u);
                    string urlAction = Url.Action("Index", "Home");
                    if (!string.IsNullOrEmpty(returnUrl))
                        urlAction = returnUrl;
                    return Json(new
                    {
                        isSuccess = true,
                        urlAction = urlAction
                    }, JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(new
                    {
                        isSuccess = false,
                        errorCode = u.BaseResult.Code,
                        message = u.BaseResult.Message,
                        urlAction = Url.Action("Login", "Account")
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            return View();
        }
        public ActionResult LogOut()
        {
            FormsAuthentication.SignOut();
            Session.Abandon();

            // clear authentication cookie
            HttpCookie cookie1 = new HttpCookie(FormsAuthentication.FormsCookieName, "");
            cookie1.Expires = DateTime.Now.AddYears(-1);
            Response.Cookies.Add(cookie1);
            //Response.Cookies.Remove(FormsAuthentication.FormsCookieName);

            // clear session cookie (not necessary for your current problem but i would recommend you do it anyway)
            HttpCookie cookie2 = new HttpCookie("ASP.NET_SessionId", "");
            cookie2.Expires = DateTime.Now.AddYears(-1);
            Response.Cookies.Add(cookie2);

            return RedirectToAction("Login");
        }
        /// <summary>
        /// 给用户建立Cookie
        /// </summary>
        /// <param name="user"></param>
        public void CreateAuthenticationTicket(User user)
        {
            string userData = serializer.Serialize(user);
            HttpCookie cookie1 = new HttpCookie("IsContinue", "");
            //cookie1.Expires = DateTime.Now.AddMinutes(30);
            Response.Cookies.Add(cookie1);

            FormsAuthenticationTicket authTicket = new FormsAuthenticationTicket(
                1, user.UserId.ToString(), DateTime.Now, DateTime.Now.AddDays(7), true, userData);
            string encTicket = FormsAuthentication.Encrypt(authTicket);
            HttpCookie faCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encTicket);
            Response.Cookies.Add(faCookie);
        }
        public ActionResult Register()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Register(User user)
        {
            BaseResult result = new BaseResult();
            user.Password = MD5Hash.CreateMD5Hash(user.Password);
            user.Status = "pending";
            user.RoleId =2;
            result = account.InsertUser(user);
            if (result.Code == 0)
            {
                return Content("<script>alert('회원가입이 완료되습니다.\\n로그인을 위해서는 관리자의 승인이 필요합니다.\\n승인이 완료될때까지 대기하기 바랍니다');window.location.href='/Account/Login';</script>");
            }
            else
            {
                return Content("<script>alert('회원가입이 실패하였습니다.');window.location.reload();</script>");
            }
        }
        [HttpPost]
        public JsonResult CheckLoginName(string loginname)
        {
            bool isSuccess=account.CheckLoginName(loginname);
            return Json(new { isSuccess = isSuccess }, JsonRequestBehavior.AllowGet);   
        }
        [HttpPost]
        public JsonResult CheckEmail(string email)
        {
            bool isSuccess = account.CheckEmail(email);
            return Json(new { isSuccess = isSuccess }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult ForgotLoginName()
        {
            return View();
        }
        [HttpPost]
        public ActionResult ForgotLoginName(string name,string email)
        {
            User u = new User();
            u.Name = name;
            u.Email = email;
            User user = account.CheckField(u);
           
            if (user.BaseResult.Code == 0)
            {
                string logo = ConfigurationManager.AppSettings["logo"].ToString();
                string title = logo + " 아이디 찾기";
                string body = string.Format(@"<p>{0}님, 안녕하세요.<p>
                            <p>{1} 아이디 찾기 메일입니다. 아이디 번호는 <b>{2}</b> 입니다.</p>
                            <p>감사합니다.</p>
                            <p>(본 메일은 발신전용 입니다.)</p>
                            ", user.Name, logo, user.LoginName);
                MailHelper.SendMail(title, body, email);
                return Json(new { isSuccess = true, errorCode = user.BaseResult.Code }, JsonRequestBehavior.AllowGet);
            }
            return Json(new { isSuccess = false,errorCode = user.BaseResult.Code }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult ForgotPassword()
        {
            return View();
        }
        [HttpPost]
        public ActionResult ForgotPassword(string loginname,string name,string email)
        {
            User u = new User();
            u.LoginName = loginname;
            u.Name = name;
            u.Email = email;
            User user = account.CheckField(u);
            if (user.BaseResult.Code == 0)
            {
                string code = Guid.NewGuid().ToString();
                BaseResult result = new BaseResult();
                result = account.GenerateCode(user.UserId, code);
                if (result.Code == 0)
                {
                    var expireTime =Convert.ToInt16(ConfigurationManager.AppSettings["GenerateCodeExpire"].ToString())/60;
                    string domain = ConfigurationManager.AppSettings["domain"].ToString();
                    string link = string.Format("{0}/Account/RetrievePassword?id={1}&code={2}", domain,user.UserId,code);
                    string logo = ConfigurationManager.AppSettings["logo"].ToString();
                    string title = logo + " 비밀번호 찾기";
                    string body = string.Format(@"<p>{0}님, 안녕하세요.<p>
                            <p>{1} 비밀번호 찾기 메일입니다. </p>
                            <p><b>비밀번호를 수정하기 위해 아래 링크를 클릭하세요. 유효기간은 {3}시간입니다.</b><p/>
                            <p>{2}</p>
                            <p>링크 클릭이 안되면 URL 전체를 복사하여 주소창에 붙여넣기 해주시기 바랍니다.<p/>
                            <p>감사합니다.</p>
                            <p>(본 메일은 발신전용 입니다.)</p>
                            ", user.Name, logo, link, expireTime.ToString());
                    MailHelper.SendMail(title, body, email);
                    return Json(new { isSuccess = true, errorCode = result.Code }, JsonRequestBehavior.AllowGet);
                }
                else {
                    return Json(new { isSuccess = false, errorCode = result.Code }, JsonRequestBehavior.AllowGet);
                }
            }
            return Json(new { isSuccess = false, errorCode = user.BaseResult.Code }, JsonRequestBehavior.AllowGet);

        }

        public ActionResult RetrievePassword(string id,string code)
        {
            ViewBag.UserId = id;
            ViewBag.Code = code;
            string expireTime = ConfigurationManager.AppSettings["GenerateCodeExpire"].ToString();
            var result = account.CheckGenerateCode(id, code, expireTime);
            if (result.Code != 0)
            {
                return Content("<script>alert('유효기간을 초과하였습니다.\\n다시 비밀번호 찾기를 신청해주세요.');window.location.href='/Account/ForgotPassword';</script>");
            }
            return View();
        }

        [HttpPost]
        public ActionResult RetrievePassword(User user, string code)
        {
            BaseResult result = new BaseResult();
            string expireTime = ConfigurationManager.AppSettings["GenerateCodeExpire"].ToString();
            var check_result = account.CheckGenerateCode(user.UserId, code, expireTime);
            if (check_result.Code == 0)
            {
                user.Password = MD5Hash.CreateMD5Hash(user.Password);
                result = account.UpdateUserInfo(user);
                if (result.Code == 0)
                {
                    return Content("<script>alert('비밀번호 설정 완료되습니다.');window.location.href='/Account/Login';</script>");
                }
                return Content("<script>alert('비밀번호 설정 실패하였습니다.';</script>");
            }
            else {
                return Content("<script>alert('유효기간을 초과하였습니다.\\n다시 비밀번호 찾기를 신청해주세요.');window.location.href='/Account/ForgotPassword';</script>");
            }
        }

        [CustomAuthorize]
        public ActionResult UserInfo(string mode)
        {
            ViewBag.Mode = mode;
            ViewBag.User = User;
            return View();
        }

        [CustomAuthorize]
        [HttpPost]
        public ActionResult UpdateInfo(User user)
        {
            if (user.Password == "123456")
                user.Password = string.Empty;
            else
                user.Password = MD5Hash.CreateMD5Hash(user.Password);

            var result = account.UpdateUserInfo(user);
            if (result.Code == 0)
            {
                var u = account.GetUserInfo(user.UserId);

                string userData = serializer.Serialize(u);
                FormsAuthenticationTicket authTicket = new FormsAuthenticationTicket(
                 1, user.UserId.ToString(), DateTime.Now, DateTime.Now.AddDays(7), true, userData);
                string encTicket = FormsAuthentication.Encrypt(authTicket);
                HttpCookie faCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encTicket);
                Response.Cookies.Add(faCookie);
                return RedirectToAction("Index", "Home");
            }

            return Content("<script>alert('회원정보를 수정 실패하였습니다.');</script>");
        }

        [CustomAuthorize(Roles = "Admin")]
        public ActionResult UserList(User user,PageData page)
        {
            var result = account.GetUserInfoList(user, page);
            ViewBag.User = result;
            return View(user);
        }

        [CustomAuthorize(Roles = "Admin")]
        public ActionResult UpdateUserStatus(string mode,string ids)
        {
            string status = "";
            if (mode == "o")
                status = "open";
            else if (mode == "c")
                status = "close";
            else {
                return Json(new { isSuccess = false, errorCode = 302, message = "파라미터를 정확히 보내주세요." }, JsonRequestBehavior.AllowGet);
            }
            var result = account.UpdateUserStatus(status, ids);
            if(result.Code==0)
            {
                return Json(new { isSuccess = true, errorCode = result.Code }, JsonRequestBehavior.AllowGet);
            }
            else {
                return Json(new { isSuccess = false, errorCode = result.Code, message=result.Message }, JsonRequestBehavior.AllowGet);
            }
        }

    }
}