using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Charity.Models 
{  
    [MetadataType(typeof(UserMetadata))]
    public partial class User
    {
        
    }
    public class UserMetadata
    {
        [DisplayName("Họ và Tên")]
        [Required(ErrorMessage = "Vui lòng nhập họ tên của bạn.")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
        public string FullName { get; set; }

        [DisplayName("Địa chỉ Email")]
        [Required(ErrorMessage = "Vui lòng nhập Email.")]
        [EmailAddress(ErrorMessage = "Định dạng Email không hợp lệ.")]
        public string Email { get; set; }

        [DisplayName("Mật khẩu")]
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên.")]
        public string PasswordHash { get; set; }

        [DisplayName("Số điện thoại")]
        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string PhoneNumber { get; set; }
    }
}