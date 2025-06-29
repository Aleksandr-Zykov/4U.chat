window.autoResizeTextarea = (element) => {
        // Reset height to auto to measure content
        element.style.height = 'auto';
        
        const minHeight = 80; // 80px minimum
        const maxHeight = 160; // 160px maximum (2x)
        const scrollHeight = element.scrollHeight;
        
        if (scrollHeight <= maxHeight) {
            // Content fits within max height - expand and hide scrollbar
            element.style.height = Math.max(minHeight, scrollHeight) + 'px';
            element.classList.remove('at-max-height');
        } else {
            // Content exceeds max height - set to max and show scrollbar
            element.style.height = maxHeight + 'px';
            element.classList.add('at-max-height');
        }
    };

    window.focusElementById = (elementId) => {
        let attempts = 0;
        const maxAttempts = 20; // Try for 2 seconds (20 * 100ms)
        
        const tryFocus = () => {
            attempts++;
            try {
                const element = document.getElementById(elementId);
                if (element) {
                    element.focus();
                    
                    // Check if focus actually worked
                    if (document.activeElement === element) {
                        console.log('Successfully focused element:', elementId, 'after', attempts, 'attempts');
                        return true; // Success
                    } else {
                        console.log('Focus attempt', attempts, '- element found but not focused. Active element:', document.activeElement?.id || 'none');
                    }
                } else {
                    console.log('Focus attempt', attempts, '- element not found:', elementId);
                }
                
                // Try again if we haven't reached max attempts
                if (attempts < maxAttempts) {
                    setTimeout(tryFocus, 100);
                } else {
                    console.error('Failed to focus element after', maxAttempts, 'attempts');
                }
                
                return false; // Continue trying
            } catch (error) {
                console.error('Error focusing element:', error);
                return false;
            }
        };
        
        // Start trying immediately
        tryFocus();
    };

    window.handleMessageInputKeyDown = (event) => {
        if (event.key === 'Enter' && !event.shiftKey) {
            // Prevent the new line from being inserted
            event.preventDefault();
            
            // Find the send button and click it
            const textarea = event.target;
            if (textarea && textarea.value.trim()) {
                // Find the send button (not the stop button) and trigger click
                const sendButton = document.querySelector('.send-button:not(.stop-button)');
                if (sendButton && !sendButton.disabled) {
                    sendButton.click();
                }
            }
        }
    };

    window.highlightCode = () => {
        console.log('Attempting to highlight code...');
        
        if (typeof Prism === 'undefined') {
            console.log('Prism.js not loaded yet');
            return;
        }

        // Simple approach: highlight all code blocks
        Prism.highlightAll();
        console.log('Prism.highlightAll() executed');
    };

    // Simple auto-highlight function
    window.autoHighlightCode = () => {
        setTimeout(window.highlightCode, 100);
    };

    // Trigger highlighting when page loads
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            setTimeout(window.highlightCode, 1000);
        });
    } else {
        setTimeout(window.highlightCode, 1000);
    }

    // Copy code to clipboard
    window.copyCode = async (codeId) => {
        try {
            const codeElement = document.getElementById(`code-${codeId}`);
            if (!codeElement) {
                console.error('Code element not found:', codeId);
                return;
            }

            const codeText = codeElement.textContent || codeElement.innerText;
            
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(codeText);
            } else {
                // Fallback for older browsers
                const textArea = document.createElement('textarea');
                textArea.value = codeText;
                textArea.style.position = 'fixed';
                textArea.style.left = '-999999px';
                textArea.style.top = '-999999px';
                document.body.appendChild(textArea);
                textArea.focus();
                textArea.select();
                document.execCommand('copy');
                textArea.remove();
            }

            // Show visual feedback
            const button = document.querySelector(`[onclick="copyCode('${codeId}')"]`);
            if (button) {
                const originalTitle = button.title;
                let feedbackTimeout;
                
                // Clear any existing feedback
                button.style.removeProperty('background');
                button.style.removeProperty('border-color');
                
                button.title = 'Copied!';
                button.style.setProperty('background', 'linear-gradient(135deg, #4CAF50 0%, #45a049 100%)', 'important');
                button.style.setProperty('border-color', '#4CAF50', 'important');
                
                // Function to clear feedback
                const clearFeedback = () => {
                    button.title = originalTitle;
                    button.style.removeProperty('background');
                    button.style.removeProperty('border-color');
                    if (feedbackTimeout) {
                        clearTimeout(feedbackTimeout);
                        feedbackTimeout = null;
                    }
                };
                
                // Clear feedback when mouse leaves
                const onMouseLeave = () => {
                    clearFeedback();
                    button.removeEventListener('mouseleave', onMouseLeave);
                };
                
                button.addEventListener('mouseleave', onMouseLeave);
                
                // Auto clear after 1.5 seconds
                feedbackTimeout = setTimeout(() => {
                    clearFeedback();
                    button.removeEventListener('mouseleave', onMouseLeave);
                }, 1500);
            }
        } catch (err) {
            console.error('Failed to copy code:', err);
        }
    };

    // Download code as file
    window.downloadCode = (codeId, language) => {
        try {
            const codeElement = document.getElementById(`code-${codeId}`);
            if (!codeElement) {
                console.error('Code element not found:', codeId);
                return;
            }

            const codeText = codeElement.textContent || codeElement.innerText;
            const fileExtension = getFileExtension(language);
            const fileName = `file.${fileExtension}`;
            
            const blob = new Blob([codeText], { type: 'text/plain' });
            const url = window.URL.createObjectURL(blob);
            
            const a = document.createElement('a');
            a.style.display = 'none';
            a.href = url;
            a.download = fileName;
            
            document.body.appendChild(a);
            a.click();
            
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);

            // Show visual feedback
            const button = document.querySelector(`[onclick="downloadCode('${codeId}', '${language}')"]`);
            if (button) {
                const originalTitle = button.title;
                let feedbackTimeout;
                
                // Clear any existing feedback
                button.style.removeProperty('background');
                button.style.removeProperty('border-color');
                
                button.title = 'Downloaded!';
                button.style.setProperty('background', 'linear-gradient(135deg, #2196F3 0%, #1976D2 100%)', 'important');
                button.style.setProperty('border-color', '#2196F3', 'important');
                
                // Function to clear feedback
                const clearFeedback = () => {
                    button.title = originalTitle;
                    button.style.removeProperty('background');
                    button.style.removeProperty('border-color');
                    if (feedbackTimeout) {
                        clearTimeout(feedbackTimeout);
                        feedbackTimeout = null;
                    }
                };
                
                // Clear feedback when mouse leaves
                const onMouseLeave = () => {
                    clearFeedback();
                    button.removeEventListener('mouseleave', onMouseLeave);
                };
                
                button.addEventListener('mouseleave', onMouseLeave);
                
                // Auto clear after 1.5 seconds
                feedbackTimeout = setTimeout(() => {
                    clearFeedback();
                    button.removeEventListener('mouseleave', onMouseLeave);
                }, 1500);
            }
        } catch (err) {
            console.error('Failed to download code:', err);
        }
    };

    // Get file extension based on language
    function getFileExtension(language) {
        switch (language.toLowerCase()) {
            case 'javascript':
            case 'js':
                return 'js';
            case 'typescript':
            case 'ts':
                return 'ts';
            case 'python':
            case 'py':
                return 'py';
            case 'csharp':
            case 'cs':
            case 'c#':
                return 'cs';
            case 'java':
                return 'java';
            case 'cpp':
            case 'c++':
                return 'cpp';
            case 'c':
                return 'c';
            case 'html':
                return 'html';
            case 'css':
                return 'css';
            case 'scss':
            case 'sass':
                return 'scss';
            case 'json':
                return 'json';
            case 'xml':
                return 'xml';
            case 'yaml':
            case 'yml':
                return 'yml';
            case 'markdown':
            case 'md':
                return 'md';
            case 'bash':
            case 'sh':
                return 'sh';
            case 'powershell':
            case 'ps1':
                return 'ps1';
            case 'sql':
                return 'sql';
            case 'php':
                return 'php';
            case 'ruby':
            case 'rb':
                return 'rb';
            case 'go':
                return 'go';
            case 'rust':
            case 'rs':
                return 'rs';
            case 'kotlin':
            case 'kt':
                return 'kt';
            case 'swift':
                return 'swift';
            case 'dart':
                return 'dart';
            case 'r':
                return 'r';
            case 'matlab':
                return 'm';
            case 'latex':
            case 'tex':
                return 'tex';
            case 'dockerfile':
                return 'dockerfile';
            case 'vue':
                return 'vue';
            case 'jsx':
                return 'jsx';
            case 'tsx':
                return 'tsx';
            default:
                return 'txt';
        }
    }

    function animateChatName(newName) {
        if (!newName) return;
        
        const elements = document.querySelectorAll('.chat-name-animating');
        
        elements.forEach(element => {
            if (!element) return;
            
            element.innerHTML = '';
            
            for (let i = 0; i < newName.length; i++) {
                const charSpan = document.createElement('span');
                charSpan.className = 'char';
                
                // Handle spaces properly by using non-breaking space
                if (newName[i] === ' ') {
                    charSpan.innerHTML = '&nbsp;';
                } else {
                    charSpan.textContent = newName[i];
                }
                
                charSpan.style.animationDelay = (i * 100) + 'ms';
                element.appendChild(charSpan);
            }
        });
    }

    // Fast chat selection without Blazor re-render
    window.setActiveChatItem = (chatId) => {
        // Remove active class from all chat items
        document.querySelectorAll('.chat-item.active').forEach(item => {
            item.classList.remove('active');
        });
        
        // Add active class to the selected chat item
        if (chatId) {
            const selectedItem = document.querySelector(`[data-chat-id="${chatId}"]`);
            if (selectedItem) {
                selectedItem.classList.add('active');
            }
        }
    };

    // Preserve and restore chat list scroll position
    window.chatListScrollPosition = window.chatListScrollPosition || 0;
    
    window.preserveChatListScroll = () => {
        const chatList = document.querySelector('.chat-list');
        if (chatList) {
            window.chatListScrollPosition = chatList.scrollTop;
        }
    };
    
    window.restoreChatListScroll = () => {
        const chatList = document.querySelector('.chat-list');
        if (chatList) {
            chatList.scrollTop = window.chatListScrollPosition;
        }
    };

    // Scroll to a specific message in the chat
    window.scrollToMessage = (messageId) => {
        if (!messageId) return;
        
        // Try multiple times as messages might still be rendering
        let attempts = 0;
        const maxAttempts = 10;
        
        const tryScroll = () => {
            attempts++;
            const messageElement = document.querySelector(`[data-message-id="${messageId}"]`);
            
            if (messageElement) {
                // Scroll the message into view with proper positioning
                const messagesContainer = document.querySelector('.messages-container');
                if (messagesContainer) {
                    const messageTop = messageElement.offsetTop;
                    const messageHeight = messageElement.offsetHeight;
                    const containerHeight = messagesContainer.clientHeight;
                    
                    // Position the message so it starts at about 1.5% from top of container
                    // This gives clean spacing and shows the full message
                    const targetScrollTop = Math.max(0, messageTop - (containerHeight * 0.015));
                    
                    messagesContainer.scrollTo({
                        top: targetScrollTop,
                        behavior: 'smooth'
                    });
                    
                    console.log('Scrolled to message:', messageId, 'at position:', targetScrollTop);
                    return true;
                }
            }
            
            // Try again if not found and haven't reached max attempts
            if (attempts < maxAttempts) {
                setTimeout(tryScroll, 100);
            } else {
                console.log('Failed to find message after', maxAttempts, 'attempts:', messageId);
            }
            
            return false;
        };
        
        // Start trying immediately
        tryScroll();
    };

    // Copy message to clipboard
    window.copyMessage = async (messageText) => {
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(messageText);
            } else {
                // Fallback for older browsers
                const textArea = document.createElement('textarea');
                textArea.value = messageText;
                textArea.style.position = 'fixed';
                textArea.style.left = '-999999px';
                textArea.style.top = '-999999px';
                document.body.appendChild(textArea);
                textArea.focus();
                textArea.select();
                document.execCommand('copy');
                textArea.remove();
            }

            // Find the hovered copy button and toggle icons
            const copyButtons = document.querySelectorAll('.copy-btn');
            copyButtons.forEach(button => {
                if (button.matches(':hover')) {
                    const copyIcon = button.querySelector('.copy-icon');
                    const checkIcon = button.querySelector('.check-icon');
                    const originalTitle = button.title;
                    
                    if (copyIcon && checkIcon) {
                        // Show checkmark, hide copy icon
                        copyIcon.style.transform = 'translate(-50%, -50%) scale(0)';
                        copyIcon.style.opacity = '0';
                        checkIcon.style.transform = 'translate(-50%, -50%) scale(1)';
                        checkIcon.style.opacity = '1';
                        
                        // Update button styling
                        button.title = 'Copied!';
                        button.style.setProperty('background', 'linear-gradient(135deg, #4CAF50 0%, #45a049 100%)', 'important');
                        button.style.setProperty('border-color', '#4CAF50', 'important');
                        button.style.setProperty('color', 'white', 'important');
                        
                        // Reset after delay
                        setTimeout(() => {
                            copyIcon.style.transform = 'translate(-50%, -50%) scale(1)';
                            copyIcon.style.opacity = '1';
                            checkIcon.style.transform = 'translate(-50%, -50%) scale(0)';
                            checkIcon.style.opacity = '0';
                            button.title = originalTitle;
                            button.style.removeProperty('background');
                            button.style.removeProperty('border-color');
                            button.style.removeProperty('color');
                        }, 2000);
                    }
                }
            });
        } catch (err) {
            console.error('Failed to copy message:', err);
        }
    };

    // File upload helper
    window.triggerFileInput = function() {
        console.log('triggerFileInput called');
        const fileInput = document.getElementById('file-input');
        console.log('fileInput element:', fileInput);
        if (fileInput) {
            console.log('Clicking file input');
            fileInput.click();
        } else {
            console.error('File input element not found');
        }
    };

    // Simple error message display
    window.displayErrorMessage = function(message) {
        alert(message);
    };

    // Citation functionality - use window properties to avoid redeclaration
    window.citationFeature = window.citationFeature || {
        button: null,
        selectedText: '',
        isInitialized: false,
        eventListenersAdded: false
    };
    
    // Function to set up Blazor handler properly
    window.setBlazorCitationHandler = function(dotNetReference) {
        console.log('Setting Blazor citation handler...');
        console.log('DotNet reference type:', typeof dotNetReference);
        console.log('DotNet reference:', dotNetReference);
        
        if (dotNetReference) {
            window.blazorCitationHandler = dotNetReference;
            console.log('✅ Blazor handler set successfully');
            console.log('Handler methods:', Object.getOwnPropertyNames(dotNetReference));
            console.log('Has invokeMethodAsync:', typeof dotNetReference.invokeMethodAsync === 'function');
            return true;
        } else {
            console.error('❌ Invalid DotNet reference provided');
            return false;
        }
    };
    
    window.initializeCitationFeature = function() {
        console.log('Initializing citation feature...');
        
        if (window.citationFeature.isInitialized) {
            console.log('Citation feature already initialized');
            return;
        }
        
        try {
            // Remove existing button if any
            if (window.citationFeature.button) {
                window.citationFeature.button.remove();
            }
            
            // Create citation button
            window.citationFeature.button = document.createElement('button');
            window.citationFeature.button.className = 'citation-button';
            window.citationFeature.button.innerHTML = `
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M3 21c3 0 7-1 7-8V5c0-1.25-.756-2.017-2-2H4c-1.25 0-2 .75-2 1.972V11c0 1.25.75 2 2 2 1 0 1 0 1 1v1c0 1-1 2-2 2s-1-.008-1-1.031"/>
                    <path d="M15 21c3 0 7-1 7-8V5c0-1.25-.757-2.017-2-2h-4c-1.25 0-2 .75-2 1.972V11c0 1.25.75 2 2 2h.75c0 2.25.25 4-2.75 4v3c0 1-1 2-2 2s-1-.008-1-1.031"/>
                </svg>
                <span>Quote</span>
            `;
            window.citationFeature.button.style.display = 'none';
            window.citationFeature.button.style.position = 'fixed';
            window.citationFeature.button.style.zIndex = '10000';
            window.citationFeature.button.title = 'Quote selected text';
            document.body.appendChild(window.citationFeature.button);
            
            console.log('Citation button created and added to body');
            
            // Add event listeners only once
            if (!window.citationFeature.eventListenersAdded) {
                // Handle selection change with more frequent checking
                document.addEventListener('mouseup', function() {
                    setTimeout(window.checkSelection, 10);
                });
                
                document.addEventListener('keyup', function() {
                    setTimeout(window.checkSelection, 10);
                });
                
                // Hide button when clicking elsewhere
                document.addEventListener('click', function(e) {
                    if (window.citationFeature.button && !window.citationFeature.button.contains(e.target)) {
                        window.hideCitationButton();
                    }
                });
                
                window.citationFeature.eventListenersAdded = true;
                console.log('Event listeners added');
            }
            
            // Handle citation button click
            window.citationFeature.button.addEventListener('click', function(e) {
                console.log('Citation button clicked');
                e.preventDefault();
                e.stopPropagation();
                window.handleCitationClick();
            });
            
            window.citationFeature.isInitialized = true;
            console.log('Citation feature initialized successfully');
            
        } catch (error) {
            console.error('Error initializing citation feature:', error);
        }
    };
    
    window.checkSelection = function() {
        try {
            const selection = window.getSelection();
            const text = selection.toString().trim();
            
            console.log('Checking selection, text length:', text.length);
            
            if (text.length > 0) {
                console.log('Selected text:', text);
                
                // Check if selection is within a message
                if (selection.rangeCount > 0) {
                    const range = selection.getRangeAt(0);
                    let element = range.commonAncestorContainer;
                    
                    // If it's a text node, get the parent element
                    if (element.nodeType === Node.TEXT_NODE) {
                        element = element.parentElement;
                    }
                    
                    // Look for message containers
                    const messageElement = element.closest('.message-text, .thinking-text, .message-content');
                    
                    console.log('Message element found:', !!messageElement);
                    
                    if (messageElement) {
                        window.citationFeature.selectedText = text;
                        window.showCitationButton(selection);
                        return;
                    }
                }
            }
            
            window.hideCitationButton();
        } catch (error) {
            console.error('Error checking selection:', error);
        }
    }
    
    window.showCitationButton = function(selection) {
        try {
            if (!window.citationFeature.button || !selection.rangeCount) {
                console.log('Cannot show citation button - missing button or selection');
                return;
            }
            
            const range = selection.getRangeAt(0);
            const rect = range.getBoundingClientRect();
            
            console.log('Selection rect:', rect);
            
            // Position button near the selection
            const left = Math.max(10, rect.left + (rect.width / 2) - 50);
            const top = Math.max(10, rect.top - 50);
            
            window.citationFeature.button.style.left = `${left}px`;
            window.citationFeature.button.style.top = `${top}px`;
            window.citationFeature.button.style.display = 'flex';
            
            console.log('Citation button shown at:', left, top);
            
        } catch (error) {
            console.error('Error showing citation button:', error);
        }
    }
    
    window.hideCitationButton = function() {
        if (window.citationFeature.button) {
            window.citationFeature.button.style.display = 'none';
            console.log('Citation button hidden');
        }
        window.citationFeature.selectedText = '';
    }
    
    window.handleCitationClick = function() {
        try {
            console.log('Handling citation click with text:', window.citationFeature.selectedText);
            console.log('Blazor handler available:', !!window.blazorCitationHandler);
            console.log('Blazor handler type:', typeof window.blazorCitationHandler);
            
            if (window.blazorCitationHandler) {
                console.log('Blazor handler methods:', Object.getOwnPropertyNames(window.blazorCitationHandler));
                console.log('Has invokeMethodAsync:', typeof window.blazorCitationHandler.invokeMethodAsync);
            }
            
            if (window.citationFeature.selectedText && window.blazorCitationHandler) {
                console.log('Calling Blazor method...');
                try {
                    window.blazorCitationHandler.invokeMethodAsync('InsertCitationFromJS', window.citationFeature.selectedText)
                        .then(() => {
                            console.log('Blazor method call completed successfully');
                        })
                        .catch((error) => {
                            console.error('Blazor method call failed:', error);
                        });
                } catch (callError) {
                    console.error('Error calling Blazor method:', callError);
                }
                window.hideCitationButton();
                window.getSelection().removeAllRanges();
            } else {
                console.error('Missing selected text or blazor handler');
                console.error('Selected text:', window.citationFeature.selectedText);
                console.error('Handler exists:', !!window.blazorCitationHandler);
            }
        } catch (error) {
            console.error('Error handling citation click:', error);
        }
    }
    
    // Test function to verify citation button works
    window.testCitationButton = function() {
        console.log('Testing citation button...');
        if (!window.citationFeature.button) {
            console.error('Citation button not found!');
            return;
        }
        
        // Show button at center of screen for testing
        window.citationFeature.button.style.left = '50%';
        window.citationFeature.button.style.top = '50%';
        window.citationFeature.button.style.transform = 'translate(-50%, -50%)';
        window.citationFeature.button.style.display = 'flex';
        window.citationFeature.selectedText = 'Test citation text';
        
        console.log('Citation button should now be visible in center of screen');
    }
    
    // Reset function for debugging
    window.resetCitationFeature = function() {
        console.log('Resetting citation feature...');
        window.citationFeature.isInitialized = false;
        window.citationFeature.eventListenersAdded = false;
        if (window.citationFeature.button) {
            window.citationFeature.button.remove();
            window.citationFeature.button = null;
        }
        
        // Try to find a Blazor handler automatically
        if (!window.blazorCitationHandler) {
            console.log('🔍 Trying to auto-detect Blazor handler...');
            const dotNetObjects = window.findBlazorHandlers();
            
            // Look for an object that has InsertCitationFromJS method
            for (const { key, obj } of dotNetObjects) {
                try {
                    // Test if this object has our method
                    if (obj && typeof obj.invokeMethodAsync === 'function') {
                        console.log(`Testing ${key} for InsertCitationFromJS method...`);
                        window.blazorCitationHandler = obj;
                        console.log(`✅ Set ${key} as blazorCitationHandler for testing`);
                        break;
                    }
                } catch (e) {
                    console.log(`❌ ${key} failed test:`, e.message);
                }
            }
        }
        
        window.initializeCitationFeature();
    }
    
    // Debug function to check current state
    window.debugCitationFeature = function() {
        console.log('=== Citation Feature Debug ===');
        console.log('Citation feature object:', window.citationFeature);
        console.log('Blazor handler:', window.blazorCitationHandler);
        console.log('Handler type:', typeof window.blazorCitationHandler);
        
        if (window.blazorCitationHandler) {
            console.log('Handler methods:', Object.getOwnPropertyNames(window.blazorCitationHandler));
            console.log('Has invokeMethodAsync:', typeof window.blazorCitationHandler.invokeMethodAsync);
        }
        
        if (window.citationFeature) {
            console.log('Is initialized:', window.citationFeature.isInitialized);
            console.log('Has button:', !!window.citationFeature.button);
            console.log('Event listeners added:', window.citationFeature.eventListenersAdded);
            console.log('Selected text:', window.citationFeature.selectedText);
        }
        
        console.log('Init function exists:', typeof window.initializeCitationFeature);
        console.log('================================');
    }
    
    // Test Blazor handler directly
    window.testBlazorHandler = function() {
        console.log('Testing Blazor handler directly...');
        if (window.blazorCitationHandler) {
            try {
                console.log('Calling InsertCitationFromJS with test text...');
                window.blazorCitationHandler.invokeMethodAsync('InsertCitationFromJS', 'TEST CITATION TEXT')
                    .then(() => {
                        console.log('✅ Blazor handler test successful!');
                    })
                    .catch((error) => {
                        console.error('❌ Blazor handler test failed:', error);
                    });
            } catch (error) {
                console.error('❌ Error calling Blazor handler:', error);
            }
        } else {
            console.error('❌ Blazor handler not found!');
            console.log('Available window properties containing "blazor":', 
                Object.keys(window).filter(key => key.toLowerCase().includes('blazor')));
        }
    }
    
    // Complete setup function for manual testing
    window.forceSetupCitation = function() {
        console.log('🔄 Force setting up citation feature...');
        
        // Reset first
        if (window.citationFeature && window.citationFeature.button) {
            window.citationFeature.button.remove();
        }
        window.citationFeature = {
            button: null,
            selectedText: '',
            isInitialized: false,
            eventListenersAdded: false
        };
        
        // Initialize
        window.initializeCitationFeature();
        
        // Check status
        window.debugCitationFeature();
        
        console.log('Note: You still need to set the Blazor handler manually or reload the page');
    }
    
    // Check for any DotNet objects in window
    window.findBlazorHandlers = function() {
        console.log('🔍 Searching for DotNet objects...');
        const dotNetObjects = [];
        
        for (const key in window) {
            try {
                const obj = window[key];
                if (obj && typeof obj === 'object' && obj.invokeMethodAsync) {
                    console.log(`Found potential DotNet object: ${key}`, obj);
                    dotNetObjects.push({ key, obj });
                }
            } catch (e) {
                // Ignore inaccessible properties
            }
        }
        
        if (dotNetObjects.length === 0) {
            console.log('❌ No DotNet objects found in window');
        } else {
            console.log(`✅ Found ${dotNetObjects.length} potential DotNet objects`);
        }
        
        return dotNetObjects;
    }