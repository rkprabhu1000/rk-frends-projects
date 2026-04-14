# Changelog

## [1.0.0] - 2026-04-10

### Added

- Initial implementation of Frends.AS4.Send task
- Support for SOAP with Attachments (SwA / Multipart/Related) message structure
- WS-Security signing with RSA-SHA256 and X.509 BinarySecurityToken
- Payload encryption using AES-256-CBC with RSA-OAEP key wrapping
- Response MIME parsing, decryption, and GZip/Deflate decompression
- eb:Messaging routing headers: Service, Action, ConversationId, MessageId, AgreementRef
- CancellationToken support throughout the task pipeline
