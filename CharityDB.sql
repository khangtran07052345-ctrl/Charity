
CREATE TABLE Categories (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(255)
);
GO
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(100) NOT NULL,
    Email VARCHAR(150) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    PhoneNumber VARCHAR(20),
    Role VARCHAR(20) DEFAULT 'User' CHECK (Role IN ('User', 'Admin')), 
    CreatedAt DATETIME DEFAULT GETDATE()
);
GO
CREATE TABLE Campaigns (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId INT NULL,
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NOT NULL,
    TargetAmount DECIMAL(18,2) NOT NULL CHECK (TargetAmount > 0),
    StartDate DATETIME NOT NULL,
    EndDate DATETIME NOT NULL,
    Status INT DEFAULT 0, 
    ImageUrl NVARCHAR(500),
    CreatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Campaigns_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE SET NULL,
    CONSTRAINT CK_Campaign_Dates CHECK (EndDate > StartDate)
);
GO
CREATE TABLE Donations (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TransactionCode VARCHAR(50) UNIQUE,
    UserId INT NOT NULL,
    CampaignId INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL CHECK (Amount > 0), 
    DonatedAt DATETIME DEFAULT GETDATE(),
    PaymentMethod NVARCHAR(50) DEFAULT N'Chuyển khoản', 
    Message NVARCHAR(500),
    Status INT DEFAULT 1,
    IsAnonymous BIT DEFAULT 0,
    CONSTRAINT FK_Donations_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_Donations_Campaigns FOREIGN KEY (CampaignId) REFERENCES Campaigns(Id)
);
GO
INSERT INTO Categories (Name, Description) VALUES
(N'Trẻ em & Giáo dục', N'Hỗ trợ học bổng, xây trường, sách vở'),
(N'Cứu trợ thiên tai', N'Khắc phục bão lũ, hạn hán, sạt lở'),
(N'Y tế & Sức khỏe', N'Hỗ trợ viện phí cho bệnh nhân hiểm nghèo');

INSERT INTO Users (FullName, Email, PasswordHash, PhoneNumber, Role) VALUES
(N'Quản trị hệ thống', 'admin@charity.com', '123456', '0901234567', 'Admin'),
(N'Trần Nguyễn Vỹ', 'vy@gmail.com', '123456', '0815456247', 'User'),
(N'Trần Lê Đăng Khang', 'manhthuongquan@gmail.com', '123456', '0988888888', 'User');

INSERT INTO Campaigns (CategoryId, Title, Description, TargetAmount, StartDate, EndDate, Status, ImageUrl) VALUES
(1, N'Áo ấm vùng cao Hà Giang', N'Mang lại mùa đông ấm áp cho hơn 500 trẻ em xã nghèo.', 30000000, GETDATE(), DATEADD(month, 2, GETDATE()), 0, '/Images/ao-am.jpg'),
(2, N'Cứu trợ đồng bào lũ lụt miền Trung', N'Nhu yếu phẩm, thuốc men và hỗ trợ sửa chữa nhà cửa sau bão.', 100000000, GETDATE(), DATEADD(month, 1, GETDATE()), 0, '/Images/bao-lu.jpg');

INSERT INTO Donations (TransactionCode, UserId, CampaignId, Amount, PaymentMethod, Message, Status, IsAnonymous) VALUES
('TXN-001', 2, 1, 500000, N'Momo', N'Chúc các em chăm ngoan học giỏi', 1, 0),
('TXN-002', 3, 1, 2000000, N'VNPay', N'Gửi các em chút quà', 1, 0),
('TXN-003', 2, 2, 1000000, N'Ngân hàng', N'Mong bà con sớm vượt qua khó khăn', 1, 1);
GO

INSERT INTO Donations (UserId, CampaignId, Amount) VALUES (1, 1, 6000000);