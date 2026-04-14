# Changelog

## [1.0.0] - 2026-04-10

### Added

- Initial implementation of Frends.AS4.Receive task
- Multipart/Related (SOAP with Attachments) MIME parsing using MimeKit
- eb:Messaging header extraction: MessageId, PartyIds, Service, Action, ConversationId, AgreementRef
- WS-Security signature verification (RSA-SHA256) against eb:Messaging and attachment
- Payload decryption using AES-256-CBC with RSA-OAEP unwrapped session key
- GZip/Deflate decompression of payload
- Synchronous AS4 receipt (eb:Receipt) generation for acknowledgement
