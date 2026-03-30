namespace ClassroomToolkit.Application.Abstractions;

public interface ISettingsStore
{
    T Load<T>() where T : class, new();
    void Save<T>(T settings) where T : class;
}
