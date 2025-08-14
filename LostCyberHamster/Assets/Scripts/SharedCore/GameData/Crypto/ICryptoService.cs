namespace Vues.GameCore
{
    /// <summary>
    /// Сервис шифрования данных.
    /// </summary>
    public interface ICryptoService
    {
        /// <summary>
        /// Зашифровать данные.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        string Encrypt(string data);

        /// <summary>
        /// Расшифровать данные.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        string Decrypt(string data);
    }
}