namespace EndfieldCharge.Contracts;

/// <summary>
/// 岛内容提供者：返回当前要展示的一帧内容；null 表示无内容可显示。
/// 仅依赖 BCL。
/// </summary>
public interface IIslandContentProvider
{
    IslandContentDescriptor? GetContent();
}
