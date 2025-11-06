using Login;
using Login.Encrypt;
using Login.Models;
using Login.Users;
using Moq;
namespace E241.Tests
{
    public class AuthTests
    {
        [Fact]
        public void Register_Success()
        {
            var encryptor = new Mock<IEncryptor>();
            var users = new Mock<IUsersList>();
            var manager = new LoginManager(encryptor.Object, users.Object);

            var login = "NewUser";
            var password = "SomePassword";
            var encLogin = "enc(NewUser)";
            var encPassword = "enc(SomePassword)";

            encryptor.Setup(e => e.Encrypt(login)).Returns(encLogin);
            encryptor.Setup(e => e.DEncrypt(login, password)).Returns(encPassword);
            users.Setup(u => u.IsValid(encLogin)).Returns(false);
            users.Setup(u => u.CreateUser(It.IsAny<User>(), null, null)).ReturnsAsync(true);

            var created = manager.CreateUser(login, password, role: null, account_id: null);

            Assert.True(created);
            users.Verify(u => u.CreateUser(
                        It.Is<User>(usr => usr.Login == encLogin && usr.Password == encPassword && usr.ID == -1),
                        null,
                        null
                    ),
                Times.Once);
        }

        [Fact]
        public void Login_Success()
        {
            var encryptor = new Mock<IEncryptor>();
            var users = new Mock<IUsersList>();
            var manager = new LoginManager(encryptor.Object, users.Object);

            var login = "ExistingLogin";
            var password = "CorrectPassword";
            var encLogin = "enc(ExistingLogin)";
            var encPassword = "enc(CorrectPassword)";
            var accountID = 12345L;

            encryptor.Setup(e => e.Encrypt(login)).Returns(encLogin);
            encryptor.Setup(e => e.DEncrypt(login, password)).Returns(encPassword);
            users.Setup(u => u.Verify(encLogin, encPassword))
                .Returns(accountID);

            var returnedAccountId = manager.Verify(login, password);
            Assert.Equal(accountID, returnedAccountId);
            users.Verify(u => u.Verify(encLogin, encPassword), Times.Once);
        }

        [Fact]
        public void Login_Fails_WithWrongPassword()
        {
            var encryptor = new Mock<IEncryptor>();
            var users = new Mock<IUsersList>();
            var manager = new LoginManager(encryptor.Object, users.Object);

            var login = "ExistingLogin";
            var wrongPassword = "WrongPassword";
            var encLogin = "enc(ExistingLogin)";
            var wrongEncPassword = "enc(WrongPassword)";

            encryptor.Setup(e => e.Encrypt(login)).Returns(encLogin);
            encryptor.Setup(e => e.DEncrypt(login, wrongPassword)).Returns(wrongEncPassword);
            users.Setup(u => u.Verify(encLogin, wrongEncPassword))
                .Returns((long?)null);

            var accountId = manager.Verify(login, wrongPassword);

            Assert.Null(accountId);
            users.Verify(u => u.Verify(encLogin, wrongEncPassword), Times.Once);
        }

        [Fact]
        public void Register_Fail_LoginCollision()
        {
            var encryptor = new Mock<IEncryptor>();
            var users = new Mock<IUsersList>();
            var manager = new LoginManager(encryptor.Object, users.Object);

            var login = "ExistingUser";
            var password = "SomePassword";
            var encLogin = "enc(ExistingUser)";

            encryptor.Setup(e => e.Encrypt(login)).Returns(encLogin);
            users.Setup(u => u.IsValid(encLogin)).Returns(true);

            var created = manager.CreateUser(login, password, role: null, account_id: null);

            Assert.False(created);
            users.Verify(u => u.CreateUser(It.IsAny<User>(), null, null), Times.Never);
        }
    }
}
