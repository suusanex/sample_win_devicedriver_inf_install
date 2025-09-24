using sample_win_devicedriver_inf_install.IntegrationTests.Fixtures;
using Xunit;

namespace sample_win_devicedriver_inf_install.IntegrationTests;

/// <summary>
/// 統合テスト用のコレクション定義
/// TestEnvironmentFixtureを共有するテストクラス群を定義します
/// </summary>
[CollectionDefinition("TestEnvironment")]
public class TestEnvironmentCollection : ICollectionFixture<TestEnvironmentFixture>
{
    // このクラスにはコードは必要ありません。
    // xUnit フレームワークがコレクション名とフィクスチャの関連付けに使用します。
}