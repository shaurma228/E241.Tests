using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

using App.Models;
using App.Data;
using Login.Data;
using Login.Users;
using Login.Models;

namespace Tests.Services {
  public class UsersListTests {
    private readonly Mock<UserDbContext> _mock_user_context;
    private readonly Mock<WorkerDbContext> _mock_worker_context;
    private readonly Mock<OfficeDbContext> _mock_office_context;
    private readonly Mock<CompanyDbContext> _mock_company_context;
    private readonly Mock<ILogger<UsersList>> _mock_logger;
    private readonly UsersList _user_list;

    public UsersListTests() {

      _mock_user_context = CreateMockContext<UserDbContext>("UserTest");
      _mock_worker_context = CreateMockContext<WorkerDbContext>("WorkerTest");
      _mock_office_context = CreateMockContext<OfficeDbContext>("OfficeTest");
      _mock_company_context = CreateMockContext<CompanyDbContext>("CompanyTest");

      /*var logger_factory = LoggerFactory.Create(builder => {
          builder.AddConsole()
          .SetMinimumLevel(LogLevel.Debug);
      });*/

      _mock_logger = new Mock<ILogger<UsersList>>();

      var mock_role_set = CreateMockDbSet<Role>(new List<Role>() {
          new Role {ID = 0, Name = "Worker"},
          new Role {ID = 1, Name = "Office"},
          new Role {ID = 2, Name = "Company"},
          new Role {ID = 3, Name = "Admin"}
      });
      _mock_user_context.Setup(p => p.Roles).Returns(mock_role_set.Object);

      _user_list = new(
          _mock_user_context.Object,
          _mock_worker_context.Object,
          _mock_office_context.Object,
          _mock_company_context.Object,
          _mock_logger.Object
          //logger_factory.CreateLogger<UsersList>()
      );
    }


  [Fact]
  public void GetUser_ExistinLogin() {
    string login = "ExistingLogin";
    User expected_user = new User {Login = login};

    var mock_set = CreateMockDbSet<User>(new List<User>(){
        new User {Login = login}
    });
    _mock_user_context.Setup(p => p.Users).Returns(mock_set.Object);

    var result = _user_list.GetUser(login);
    Assert.NotNull(result);
    Assert.Equal(expected_user.Login, result.Login);
    Assert.Equal(expected_user.ID, result.ID);
  }

  [Fact]
  public async Task CreateUser_ValidData_Worker() {
    User user = new User {
      ID = -1,
      Login = "NewUser",
      Password = "Password",
      Role = {ID=0, Name="Worker"}};

    var mock_user_set = CreateMockDbSet<User>(new List<User>()); 
    var mock_worker_set = CreateMockDbSet<Worker>(new List<Worker>());

    _mock_user_context.Setup(p => p.Users).Returns(mock_user_set.Object);
    _mock_worker_context.Setup(p => p.Workers).Returns(mock_worker_set.Object);

    var result = await _user_list.CreateUser(user, -1, -1);
    Assert.True(result);
  }

  [Fact]
  public async Task CreateUser_ValidData_Office() {
    User user = new User {
      ID = 1,
      Login = "NewOfficeUser",
      Password = "Password",
      Role = {ID=1, Name="Office"}};
    
    var mock_user_set = CreateMockDbSet<User>(new List<User>());
    var mock_office_set = CreateMockDbSet<Office>(new List<Office>(){
        new Office {
        ID = 1,
        CompanyID = 2}
    });
    var mock_company_set = CreateMockDbSet<Company>(new List<Company>(){
        new Company {
        ID = 2}
    });

    _mock_user_context.Setup(p => p.Users).Returns(mock_user_set.Object);
    _mock_office_context.Setup(p => p.Offices).Returns(mock_office_set.Object);
    _mock_company_context.Setup(p => p.Companies).Returns(mock_company_set.Object);

    var result = await _user_list.CreateUser(user, 2, 1);
    Assert.True(result);
  }



  private Mock<TContext> CreateMockContext<TContext>(string dbname) where TContext : DbContext {
    var options = new DbContextOptionsBuilder<TContext>()
      .UseInMemoryDatabase(databaseName: dbname)
      .Options;
    return new Mock<TContext>(options);
  }

  private Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class {
    var queryable = data.AsQueryable();
    var mock_set = new Mock<DbSet<T>>();

    mock_set.As<IQueryable<T>>().Setup(p => p.Provider).Returns(queryable.Provider);
    mock_set.As<IQueryable<T>>().Setup(p => p.Expression).Returns(queryable.Expression);
    mock_set.As<IQueryable<T>>().Setup(p => p.ElementType).Returns(queryable.ElementType);
    mock_set.As<IQueryable<T>>().Setup(p => p.GetEnumerator()).Returns(() => queryable.GetEnumerator());

    mock_set.As<IAsyncEnumerable<T>>()
      .Setup(p => p.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
      .Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));
    mock_set.As<IQueryable<T>>()
      .Setup(p => p.Provider)
      .Returns(new TestAsyncQueryProvider<T>(queryable.Provider));

    return mock_set;
  }



  internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T> {
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner)
      => _inner = inner;

    public ValueTask<bool> MoveNextAsync() =>
      new ValueTask<bool>(_inner.MoveNext());

    public T Current =>
      _inner.Current;

    public ValueTask DisposeAsync() {
      _inner.Dispose();
      return ValueTask.CompletedTask;
    }
  }

  internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider {
    private readonly IQueryProvider _inner;

    internal TestAsyncQueryProvider(IQueryProvider inner) =>
      _inner = inner;

    public IQueryable CreateQuery(Expression expression) =>
      new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
      new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression) =>
      _inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) =>
      _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancel_tok = default) {
      var expected_result_type = typeof(TResult).GetGenericArguments()[0];
      var execution_result = typeof(IQueryProvider)
        .GetMethod(
            name: nameof(IQueryProvider.Execute),
            genericParameterCount: 1,
            types: new[] {typeof(Expression)})
        .MakeGenericMethod(expected_result_type)
        .Invoke(this, new[] {expression});

      return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))
        .MakeGenericMethod(expected_result_type)
        .Invoke(null, new[] {execution_result});
    }

    internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T> {
      public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) {}

      public TestAsyncEnumerable(Expression expression) : base(expression) {}

      public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancel_tok = default) =>
        new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

      IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }
  }
} }
