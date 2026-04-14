# Changelog

## [1.0.0] - 2026-04-13

### Added

- Initial implementation of Frends.AS4.PullRequest task
- Inbound eb:PullRequest signal parsing — extracts MPC and PullRequest MessageId
- Synchronous UserMessage response building (Multipart/Related SOAP with Attachments)
- WS-Security RSA-SHA256 signing of the response UserMessage covering eb:Messaging and attachment
- AES-256-CBC payload encryption with RSA-OAEP session key wrapping for the response
- EBMS:0006 EmptyMessagePartitionChannel error signal when no payload is provided
- eb:Receipt signal parsing from the partner's acknowledgement
- CancellationToken support throughout
