![image](https://github.com/user-attachments/assets/c8252476-933e-4cec-8ee5-1753849d5d18)
# MagicChatbox Software License Agreement

**IMPORTANT:** This software is released under a custom source-available **proprietary** license (the "License"). It is **not** an Open Source Initiative (OSI) approved license. This License allows public access to the source code and grants limited rights to use and modify the software, but with **strict restrictions on core modules** and the requirement to retain the accompanying Terms of Service (TOS).

## 1. Definitions
**"Software"** refers to the MagicChatbox software by BoiHanny, including all its components, source code, and related documentation.  
**"Licensor"** refers to BoiHanny, the creator and copyright holder of the Software.  
**"Licensee"** refers to any individual or entity who uses, copies, modifies, or distributes the Software under this License.  
**"Protected Components"** refers to specific integral parts of the Software that are subject to additional restrictions under this License. The Protected Components are:  
- The **Monitoring and Ban API** of the Software used for moderation in VRChat.  
- The **Pulsoid Heart Rate Integration Module**, which connects the Software exclusively to the Pulsoid heart rate streaming service.  
**"Fork" or "Derivative Work"** means any software or project that is based on or derived from the Software, including any modifications or adaptations of the Software’s source code.  
**"TOS"** refers to the MagicChatbox Terms of Service, currently located at [`Security.md`](https://github.com/BoiHanny/vrcosc-magicchatbox/blob/master/Security.md), which is incorporated by reference into this License.

## 2. Grant of License
Subject to full compliance with all terms and conditions of this License, Licensor hereby grants Licensee a worldwide, royalty-free, non-exclusive, non-transferable permission to use, copy, modify (with limitations described herein), and distribute the Software and its source code. This grant is similar in scope to the MIT License but **expressly limited by the restrictions and conditions** outlined in this License. No other uses are permitted beyond what is expressly allowed.

## 3. Permitted Use and Limited Modifiability
Licensee may use and modify the Software for personal or commercial purposes, and may create Forks or Derivative Works, **with the exception of the Protected Components**.  
- **General Modifications:** Licensee may alter or improve general components of the Software (e.g., UI, non-core features) for their own use or in Forks, **provided that** such modifications do not remove, disable, bypass, or otherwise interfere with any Protected Components.  
- **Prohibition on Circumvention:** Under no circumstances shall Licensee (or any third party) attempt to extend, replace, or circumvent the functionality of the Protected Components. Any modification that bypasses or duplicates the functions of the Monitoring and Ban API or the Pulsoid integration (for example, replacing the Pulsoid service with a different heart rate streaming service) is **strictly prohibited** and will be considered a material breach of this License.

## 4. Protected Monitoring and Ban API
The Software includes a **Monitoring and Ban API** intended for moderation and user safety in VRChat. This component is a Protected Component under this License:  
- **No Alteration or Removal:** Licensee **shall not** alter, remove, disable, or bypass the Monitoring and Ban API in any copy of the Software, Fork, or Derivative Work. This API must remain fully intact, operational, and unmodified to ensure proper moderation functionality.  
- **Enforcement:** Any breach of this section (e.g., modifying or circumventing the ban/monitoring functionality) will result in immediate termination of the License (see Section 10) and may subject the Licensee to legal action by the Licensor, including but not limited to injunctions or claims for damages.

## 5. Protected Pulsoid Integration
The Software includes an integration with the **Pulsoid** heart rate streaming service as a core feature, which is designated as a Protected Component:  
- **Exclusive Integration:** The Software is **designed to integrate exclusively with the Pulsoid heart rate streaming service**. Licensee shall not remove, replace, extend, or modify this Pulsoid integration. For example, substituting Pulsoid with another heart rate streaming service or altering the way Pulsoid is used in the Software is expressly forbidden.  
- **No Alternative Services:** Licensee is prohibited from using or integrating any alternative heart rate streaming service or module in place of the Pulsoid module within any Fork or Derivative Work.  
- **Enforcement:** Any violation of this section (such as attempting to use a different heart rate service or altering the Pulsoid module) constitutes a material breach of this License and will trigger immediate termination of Licensee’s rights (see Section 10). The Licensor reserves the right to enforce this provision through **DMCA takedown notices** and other legal actions to prevent unauthorized modifications or distributions.

## 6. Forking and Distribution Requirements
Licensee is permitted to fork the repository and create or distribute Derivative Works **only if** the following conditions are met:

1. **Visible Repository Link**  
   Any Fork or Derivative Work must include a clear and visible notice that it is based on the MagicChatbox Software. This includes providing a prominent link back to the official MagicChatbox repository (for example, in the project’s README or documentation).

2. **Retention of License and TOS**  
   The forked or modified project must retain this MagicChatbox License Agreement **and** the original MagicChatbox [Terms of Service](https://github.com/BoiHanny/vrcosc-magicchatbox/blob/master/Security.md) within its repository or distribution. These documents should be easily accessible to anyone obtaining the Fork or Derivative Work. Under no circumstances may the TOS be removed, obscured, or modified in a way that diminishes its visibility or effect.

3. **Pulsoid Terms and Documentation**  
   All Forks or Derivative Works must include references or links to Pulsoid’s own Terms of Service and relevant documentation, acknowledging that the heart rate functionality is provided through Pulsoid and is subject to Pulsoid’s terms.

4. **No Removal of Notices**  
   Licensee must not remove or obscure any legal notices, attributions, or documentation included with the Software that pertain to the Licensor, this License, the TOS, or the Pulsoid integration.

Failure to meet **any** of the above conditions for forking or distribution will be considered a breach of the License, subject to termination and enforcement as outlined in Section 10.

## 7. Attribution
All copies, Forks, or distributions of the Software (including any modified versions) must include the following attribution in a prominent location (for example, in documentation, an "About" dialog, or the README file):

> “This product includes software developed by BoiHanny / MagicChatbox, designed to integrate exclusively with the Pulsoid heart rate streaming service.”

This attribution notice must remain intact and unaltered. Its purpose is to give credit to the original developer (BoiHanny/MagicChatbox) and to clearly indicate the exclusive integration with Pulsoid. Removal or modification of this notice is a violation of this License.

## 8. Disclaimer of Warranty
**No Warranty:** The Software is provided on an “**AS IS**” and “**AS AVAILABLE**” basis, without warranty of any kind. The Licensor disclaims all warranties, express or implied, including but not limited to the implied warranties of merchantability, fitness for a particular purpose, and non-infringement. The entire risk as to the quality and performance of the Software is borne by the Licensee. The Licensor does not guarantee that the Software will meet the Licensee’s requirements or that the operation of the Software will be uninterrupted or error-free.

## 9. Limitation of Liability
**Limited Liability:** In no event and under no legal theory (whether in contract, tort, negligence, or otherwise) shall the Licensor or any contributors to the Software be liable for any direct, indirect, incidental, special, exemplary, or consequential damages; nor for any loss of revenue, profits, or data; nor for any claim, damage, or other liability arising from the use of, or inability to use, the Software; even if the Licensor has been advised of the possibility of such damages. Licensee acknowledges that the Licensee’s use of the Software is at their own discretion and risk, and that Licensee will be solely responsible for any damage that results from the use of the Software.

## 10. Termination and Enforcement
This License (and the rights granted herein) will **immediately terminate** upon any breach by the Licensee of the terms and conditions stated above, without prior notice. If the Licensee violates any provision of this License (including but not limited to altering Protected Components or failing to comply with distribution requirements), then:

- All permissions and rights granted to the Licensee under this License are automatically revoked. The Licensee must immediately cease any use, distribution, or development of the Software or any Forks/Derivatives thereof.  
- The Licensor may pursue any and all legal remedies available for violation of these terms or infringement of intellectual property rights. This includes, but is not limited to, seeking injunctive relief, claims for damages, and issuing **DMCA takedown** notices to remove or disable access to any infringing Forks or distributions.  
- The Licensee shall, upon termination, destroy or remove all copies of the Software and any Derivative Works in their possession or control, if requested by the Licensor.

The Licensee understands and agrees that any breach of the provisions regarding Protected Components (Sections 4 and 5) would cause irreparable harm to the Licensor for which monetary damages may be inadequate. Accordingly, the Licensor is entitled to seek immediate injunctive relief (in addition to any other remedies available at law or in equity) to enforce these sections.

## 11. General Provisions
- **License Non-Transferable:** Licensee may not assign or transfer this License or any rights granted herein without the prior written consent of the Licensor.  
- **No Waiver:** The failure of the Licensor to enforce any provision of this License shall not constitute a waiver of that provision or any other provision, nor limit the Licensor’s right to enforce the same provision in the future.  
- **Severability:** If any provision of this License is held to be invalid, illegal, or unenforceable by a court of competent jurisdiction, that provision shall be deemed modified to the minimum extent necessary to make it enforceable, and the remainder of the License shall remain in full force and effect.

## 12. Acceptance
By downloading, installing, using, or distributing the Software, the Licensee acknowledges that they have read this License, understand it, **agree to be bound by all its terms and conditions**, and will also adhere to the MagicChatbox [Terms of Service](https://github.com/BoiHanny/vrcosc-magicchatbox/blob/master/Security.md). If the Licensee does not agree to these terms, they must not use, modify, or distribute the Software.

---

**Copyright (c) 2025 BoiHanny / MagicChatbox. All rights reserved.**

*Note: The [MagicChatbox Terms of Service](https://github.com/BoiHanny/vrcosc-magicchatbox/blob/master/Security.md) form an integral part of this License and must remain unaltered and included in any distribution, fork, or derivative.*
