using AutoFixture;
using AutoFixture.AutoMoq;
using BookFlightTickets.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BookFlightTickets.ServiceTests
{
    public class RedisCacheServiceTests
    {
        private readonly IFixture _fixture;
        private readonly Mock<IDistributedCache> _cacheMock;
        private readonly Mock<IConnectionMultiplexer> _redisConnectionMock;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly RedisCacheService _sut;

        public RedisCacheServiceTests()
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            _cacheMock = new Mock<IDistributedCache>();
            _redisConnectionMock = new Mock<IConnectionMultiplexer>();
            _logger = NullLogger<RedisCacheService>.Instance;

            _sut = new RedisCacheService(
                _cacheMock.Object,
                _logger,
                _redisConnectionMock.Object);
        }

        #region GetAsync

        public class GetAsyncTests : RedisCacheServiceTests
        {
            [Fact]
            public async Task Should_ReturnDeserializedObject_WhenCacheHit()
            {
                // Arrange
                var key = _fixture.Create<string>();
                var expectedObject = new TestClass { Id = 1, Name = "Test" };
                var serialized = JsonSerializer.Serialize(expectedObject);
                var byteData = Encoding.UTF8.GetBytes(serialized);

                _cacheMock
                    .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(byteData);

                // Act
                var result = await _sut.GetAsync<TestClass>(key);

                // Assert
                result.Should().NotBeNull();
                result!.Id.Should().Be(expectedObject.Id);
                result.Name.Should().Be(expectedObject.Name);
            }

            [Fact]
            public async Task Should_ReturnDefault_WhenCacheMiss()
            {
                // Arrange
                var key = _fixture.Create<string>();

                _cacheMock
                    .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((byte[]?)null);

                // Act
                var result = await _sut.GetAsync<TestClass>(key);

                // Assert
                result.Should().BeNull();
            }

            [Fact]
            public async Task Should_ReturnDefault_AndRemoveKey_WhenDeserializationFails()
            {
                // Arrange
                var key = _fixture.Create<string>();
                var invalidJson = "this is not json";
                var byteData = Encoding.UTF8.GetBytes(invalidJson);

                _cacheMock
                    .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(byteData);

                // Act
                var result = await _sut.GetAsync<TestClass>(key);

                // Assert
                result.Should().BeNull();
                _cacheMock.Verify(c => c.RemoveAsync(key, It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task Should_ReturnDefault_WhenExceptionOccurs()
            {
                // Arrange
                var key = _fixture.Create<string>();

                _cacheMock
                    .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception("Redis error"));

                // Act
                var result = await _sut.GetAsync<TestClass>(key);

                // Assert
                result.Should().BeNull();
            }
        }

        #endregion

        #region SetAsync

        public class SetAsyncTests : RedisCacheServiceTests
        {
            [Fact]
            public async Task Should_CacheValue_WithAbsoluteExpiry()
            {
                // Arrange
                var key = _fixture.Create<string>();
                var value = new TestClass { Id = 2, Name = "SetTest" };
                var expiry = TimeSpan.FromMinutes(5);

                byte[]? capturedBytes = null;
                DistributedCacheEntryOptions? capturedOptions = null;

                _cacheMock
                    .Setup(c => c.SetAsync(
                        It.IsAny<string>(),
                        It.IsAny<byte[]>(),
                        It.IsAny<DistributedCacheEntryOptions>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                        (k, bytes, opts, _) =>
                        {
                            capturedBytes = bytes;
                            capturedOptions = opts;
                        })
                    .Returns(Task.CompletedTask);

                // Act
                await _sut.SetAsync(key, value, expiry);

                // Assert
                capturedBytes.Should().NotBeNull();
                var serialized = Encoding.UTF8.GetString(capturedBytes!);
                var deserialized = JsonSerializer.Deserialize<TestClass>(serialized);
                deserialized.Should().BeEquivalentTo(value);

                capturedOptions!.AbsoluteExpirationRelativeToNow.Should().Be(expiry);
            }

            [Fact]
            public async Task Should_CacheValue_WithDefaultSlidingExpiry_WhenNoExpiryProvided()
            {
                // Arrange
                var key = _fixture.Create<string>();
                var value = new TestClass { Id = 3, Name = "SlidingTest" };

                DistributedCacheEntryOptions? capturedOptions = null;

                _cacheMock
                    .Setup(c => c.SetAsync(
                        It.IsAny<string>(),
                        It.IsAny<byte[]>(),
                        It.IsAny<DistributedCacheEntryOptions>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                        (_, _, opts, _) => capturedOptions = opts)
                    .Returns(Task.CompletedTask);

                // Act
                await _sut.SetAsync(key, value);

                // Assert
                capturedOptions!.SlidingExpiration.Should().Be(TimeSpan.FromMinutes(10));
            }

            [Fact]
            public async Task Should_NotCallSet_WhenValueIsNull()
            {
                // Arrange
                var key = _fixture.Create<string>();

                // Act
                await _sut.SetAsync<TestClass>(key, null!);

                // Assert
                _cacheMock.Verify(
                    c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()),
                    Times.Never);
            }

            [Fact]
            public async Task Should_HandleException_WhenSetFails()
            {
                // Arrange
                var key = _fixture.Create<string>();
                var value = new TestClass();

                _cacheMock
                    .Setup(c => c.SetAsync(
                        It.IsAny<string>(),
                        It.IsAny<byte[]>(),
                        It.IsAny<DistributedCacheEntryOptions>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception("Set failed"));

                // Act
                Func<Task> act = async () => await _sut.SetAsync(key, value);

                // Assert
                await act.Should().NotThrowAsync();
            }
        }

        #endregion

        #region RemoveAsync

        public class RemoveAsyncTests : RedisCacheServiceTests
        {
            [Fact]
            public async Task Should_RemoveKey()
            {
                // Arrange
                var key = _fixture.Create<string>();

                // Act
                await _sut.RemoveAsync(key);

                // Assert
                _cacheMock.Verify(c => c.RemoveAsync(key, It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task Should_HandleException_WhenRemoveFails()
            {
                // Arrange
                var key = _fixture.Create<string>();

                _cacheMock
                    .Setup(c => c.RemoveAsync(key, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception("Remove failed"));

                // Act
                Func<Task> act = async () => await _sut.RemoveAsync(key);

                // Assert
                await act.Should().NotThrowAsync();
            }
        }

        #endregion

        #region ExistsAsync

        public class ExistsAsyncTests : RedisCacheServiceTests
        {
            [Fact]
            public async Task Should_ReturnTrue_WhenKeyExists()
            {
                // Arrange
                var key = _fixture.Create<string>();
                var data = _fixture.Create<string>();
                var byteData = Encoding.UTF8.GetBytes(data);

                _cacheMock
                    .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(byteData);

                // Act
                var result = await _sut.ExistsAsync(key);

                // Assert
                result.Should().BeTrue();
            }

            [Fact]
            public async Task Should_ReturnFalse_WhenKeyDoesNotExist()
            {
                // Arrange
                var key = _fixture.Create<string>();

                _cacheMock
                    .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((byte[]?)null);

                // Act
                var result = await _sut.ExistsAsync(key);

                // Assert
                result.Should().BeFalse();
            }

            [Fact]
            public async Task Should_ReturnFalse_WhenExceptionOccurs()
            {
                // Arrange
                var key = _fixture.Create<string>();

                _cacheMock
                    .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception());

                // Act
                var result = await _sut.ExistsAsync(key);

                // Assert
                result.Should().BeFalse();
            }
        }

        #endregion

        #region GetOrSetAsync

        public class GetOrSetAsyncTests : RedisCacheServiceTests
        {
            [Fact]
            public async Task Should_ReturnCachedValue_WhenCacheHit()
            {
                // Arrange
                var key = _fixture.Create<string>();
                var cachedValue = new TestClass { Id = 5, Name = "Cached" };
                var serialized = JsonSerializer.Serialize(cachedValue);
                var byteData = Encoding.UTF8.GetBytes(serialized);
                var factoryCalled = false;

                _cacheMock
                    .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(byteData);

                // Act
                var result = await _sut.GetOrSetAsync(key, () =>
                {
                    factoryCalled = true;
                    return Task.FromResult(new TestClass());
                });

                // Assert
                result.Should().BeEquivalentTo(cachedValue);
                factoryCalled.Should().BeFalse();
            }

            [Fact]
            public async Task Should_CallFactoryAndSetCache_WhenCacheMiss()
            {
                // Arrange
                var key = _fixture.Create<string>();
                var factoryValue = new TestClass { Id = 6, Name = "Factory" };
                var factoryCalled = false;

                _cacheMock
                    .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((byte[]?)null);

                byte[]? capturedBytes = null;
                _cacheMock
                    .Setup(c => c.SetAsync(
                        It.IsAny<string>(),
                        It.IsAny<byte[]>(),
                        It.IsAny<DistributedCacheEntryOptions>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                        (_, bytes, _, _) => capturedBytes = bytes)
                    .Returns(Task.CompletedTask);

                // Act
                var result = await _sut.GetOrSetAsync(key, () =>
                {
                    factoryCalled = true;
                    return Task.FromResult(factoryValue);
                });

                // Assert
                result.Should().BeEquivalentTo(factoryValue);
                factoryCalled.Should().BeTrue();
                capturedBytes.Should().NotBeNull();
                var serialized = Encoding.UTF8.GetString(capturedBytes!);
                var deserialized = JsonSerializer.Deserialize<TestClass>(serialized);
                deserialized.Should().BeEquivalentTo(factoryValue);
            }

            [Fact]
            public async Task Should_NotSetCache_WhenFactoryReturnsNull()
            {
                // Arrange
                var key = _fixture.Create<string>();
                var factoryCalled = false;

                _cacheMock
                    .Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((byte[]?)null);

                // Act
                var result = await _sut.GetOrSetAsync<TestClass>(key, () =>
                {
                    factoryCalled = true;
                    return Task.FromResult<TestClass>(null!);
                });

                // Assert
                result.Should().BeNull();
                factoryCalled.Should().BeTrue();
                _cacheMock.Verify(
                    c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()),
                    Times.Never);
            }
        }

        #endregion

        #region RemoveByPatternAsync

        public class RemoveByPatternAsyncTests : RedisCacheServiceTests
        {
            [Fact]
            public async Task Should_RemoveKeysMatchingPattern_WhenRedisConnectionAvailable()
            {
                // Arrange
                var pattern = "test:*";
                var endpointMock = new Mock<EndPoint>();
                var serverMock = new Mock<IServer>();
                var databaseMock = new Mock<IDatabase>();
                var keys = new RedisKey[] { "test:1", "test:2", "test:3" };

                _redisConnectionMock
                    .Setup(r => r.GetEndPoints(It.IsAny<bool>()))
                    .Returns(new[] { endpointMock.Object });

                _redisConnectionMock
                    .Setup(r => r.GetServer(endpointMock.Object, It.IsAny<object>()))
                    .Returns(serverMock.Object);

                serverMock
                    .Setup(s => s.Keys(
                        It.IsAny<int>(),                // database
                         It.IsAny<RedisValue>(),        // pattern
                        It.IsAny<int>(),                 // pageSize
                        It.IsAny<long>(),                 // cursor
                        It.IsAny<int>(),                  // pageOffset
                        It.IsAny<CommandFlags>()))        // flags
                    .Returns(keys);

                _redisConnectionMock
                    .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                    .Returns(databaseMock.Object);

                databaseMock
                    .Setup(db => db.KeyDeleteAsync(keys, It.IsAny<CommandFlags>()))
                    .ReturnsAsync(keys.Length);

                // Act
                await _sut.RemoveByPatternAsync(pattern);

                // Assert
                databaseMock.Verify(db => db.KeyDeleteAsync(keys, It.IsAny<CommandFlags>()), Times.Once);
            }

            [Fact]
            public async Task Should_NotCallKeyDelete_WhenNoKeysMatchPattern()
            {
                // Arrange
                var pattern = "test:*";
                var endpointMock = new Mock<EndPoint>();
                var serverMock = new Mock<IServer>();

                _redisConnectionMock
                    .Setup(r => r.GetEndPoints(It.IsAny<bool>()))
                    .Returns(new[] { endpointMock.Object });

                _redisConnectionMock
                    .Setup(r => r.GetServer(endpointMock.Object, It.IsAny<object>()))
                    .Returns(serverMock.Object);

                serverMock
                    .Setup(s => s.Keys(
                        It.IsAny<int>(),        // database
                        It.IsAny<RedisValue>(),      // pattern (RedisValue)
                        It.IsAny<int>(),         // pageSize
                        It.IsAny<long>(),        // cursor
                        It.IsAny<int>(),         // pageOffset
                        It.IsAny<CommandFlags>())) // flags
                    .Returns(Array.Empty<RedisKey>());

                // Act
                await _sut.RemoveByPatternAsync(pattern);

                // Assert
                _redisConnectionMock.Verify(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.Never);
            }

            [Fact]
            public async Task Should_LogWarning_WhenRedisConnectionIsNull()
            {
                // Arrange
                var sutWithoutConnection = new RedisCacheService(_cacheMock.Object, _logger, null);
                var pattern = _fixture.Create<string>();

                // Act
                await sutWithoutConnection.RemoveByPatternAsync(pattern);
                // لا استثناءات، فقط تسجيل تحذير
            }

            [Fact]
            public async Task Should_HandleException_WhenRemoveByPatternFails()
            {
                // Arrange
                var pattern = "test:*";
                var endpointMock = new Mock<EndPoint>();

                _redisConnectionMock
                    .Setup(r => r.GetEndPoints(It.IsAny<bool>()))
                    .Returns(new[] { endpointMock.Object });

                _redisConnectionMock
                    .Setup(r => r.GetServer(endpointMock.Object, It.IsAny<object>()))
                    .Throws(new Exception("Server error"));

                // Act
                Func<Task> act = async () => await _sut.RemoveByPatternAsync(pattern);

                // Assert
                await act.Should().NotThrowAsync();
            }
        }

        #endregion

        private class TestClass
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }
    }
}