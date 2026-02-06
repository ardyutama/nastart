# Week 6: PaddleOCR Integration 📷

> **Goal**: Integrate PaddleOCR for receipt scanning — set up the Python microservice with FastAPI, create the HTTP client in .NET, build the `ScanReceipt` feature slice, and implement fuzzy matching for ingredient detection.

---

## Table of Contents
1. [Day 1: PaddleOCR Python Microservice Setup](#day-1-paddleocr-python-microservice-setup)
2. [Day 2: FastAPI Receipt Scanning Endpoint](#day-2-fastapi-receipt-scanning-endpoint)
3. [Day 3: PaddleOcrService HTTP Client](#day-3-paddleocrservice-http-client)
4. [Day 4: ScanReceipt Feature Slice](#day-4-scanreceipt-feature-slice)
5. [Day 5: FuzzyMatchingService for Ingredients](#day-5-fuzzymatchingservice-for-ingredients)
6. [Day 6: Error Handling & Retry Policies](#day-6-error-handling--retry-policies)
7. [Day 7: Testing with Real Receipts](#day-7-testing-with-real-receipts)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: PaddleOCR Python Microservice Setup

## 🧒 Explain Like I'm 5

Imagine you have a magic camera 📸 that can READ text from pictures! You take a photo of your grocery receipt, and the camera tells you:
- "Flour — Rp 25.000"
- "Sugar — Rp 16.000"
- "Eggs — Rp 35.000"

**PaddleOCR** is that magic camera — it's a smart computer program that can read text from any image!

We put PaddleOCR in a separate box (microservice) because:
- It needs special Python tools
- It uses lots of memory for the AI brain
- We can restart or upgrade it without touching our main app

## 🔧 Engineer Language

**PaddleOCR** is an open-source OCR toolkit by Baidu that supports 80+ languages including Indonesian. We deploy it as a **FastAPI microservice** to:
- Isolate Python dependencies from .NET
- Scale OCR independently
- Use GPU acceleration if available

> 📖 **Microsoft Docs**: *"Microservices should be small and focused on doing one thing well. They communicate over well-defined APIs."*
>
> — [.NET Microservices Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/)

### Project Structure:

```
nastart/
├── backend/                    # .NET API
│   └── src/Nastart.Api/
│
├── ocr-service/               # Python OCR microservice
│   ├── app/
│   │   ├── __init__.py
│   │   ├── main.py            # FastAPI app
│   │   ├── ocr.py             # PaddleOCR wrapper
│   │   ├── models.py          # Pydantic models
│   │   └── preprocessing.py   # Image preprocessing
│   ├── requirements.txt
│   ├── Dockerfile
│   └── .env.example
│
└── docker-compose.yml         # Orchestrates all services
```

### Create OCR Service Directory:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart

# Create OCR service structure
mkdir ocr-service
mkdir ocr-service\app

# Create Python files
New-Item ocr-service\app\__init__.py
New-Item ocr-service\app\main.py
New-Item ocr-service\app\ocr.py
New-Item ocr-service\app\models.py
New-Item ocr-service\app\preprocessing.py
New-Item ocr-service\requirements.txt
New-Item ocr-service\Dockerfile
New-Item ocr-service\.env.example
```

### requirements.txt:

Create `ocr-service/requirements.txt`:

```txt
fastapi==0.109.0
uvicorn[standard]==0.27.0
python-multipart==0.0.6
paddlepaddle==2.6.0
paddleocr==2.7.3
pillow==10.2.0
numpy==1.26.3
pydantic==2.5.3
pydantic-settings==2.1.0
httpx==0.26.0
```

### Pydantic Models:

Create `ocr-service/app/models.py`:

```python
"""
Pydantic models for OCR service request/response.
"""
from pydantic import BaseModel, Field
from typing import List, Optional
from enum import Enum


class OcrStatus(str, Enum):
    SUCCESS = "success"
    PARTIAL = "partial"
    FAILED = "failed"


class BoundingBox(BaseModel):
    """Coordinates of detected text region."""
    x1: int = Field(..., description="Top-left X")
    y1: int = Field(..., description="Top-left Y")
    x2: int = Field(..., description="Bottom-right X")
    y2: int = Field(..., description="Bottom-right Y")


class TextLine(BaseModel):
    """A single line of detected text."""
    text: str = Field(..., description="Detected text content")
    confidence: float = Field(..., ge=0, le=1, description="OCR confidence 0-1")
    bounding_box: BoundingBox = Field(..., description="Text location in image")
    
    class Config:
        json_schema_extra = {
            "example": {
                "text": "Tepung Terigu 1kg",
                "confidence": 0.95,
                "bounding_box": {"x1": 10, "y1": 100, "x2": 200, "y2": 130}
            }
        }


class ReceiptLine(BaseModel):
    """A parsed receipt line item."""
    raw_text: str = Field(..., description="Original OCR text")
    item_name: Optional[str] = Field(None, description="Extracted item name")
    quantity: Optional[float] = Field(None, description="Extracted quantity")
    unit: Optional[str] = Field(None, description="Extracted unit (kg, pcs, etc)")
    price: Optional[float] = Field(None, description="Extracted price")
    confidence: float = Field(..., description="Overall confidence for this line")


class OcrRequest(BaseModel):
    """Request to process a receipt image."""
    image_base64: Optional[str] = Field(None, description="Base64 encoded image")
    image_url: Optional[str] = Field(None, description="URL to fetch image from")
    language: str = Field(default="id", description="OCR language code")
    
    class Config:
        json_schema_extra = {
            "example": {
                "image_base64": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
                "language": "id"
            }
        }


class OcrResponse(BaseModel):
    """Response from OCR processing."""
    status: OcrStatus = Field(..., description="Processing status")
    raw_lines: List[TextLine] = Field(default_factory=list, description="All detected text lines")
    receipt_lines: List[ReceiptLine] = Field(default_factory=list, description="Parsed receipt items")
    total_detected: Optional[float] = Field(None, description="Detected total amount if found")
    shop_name: Optional[str] = Field(None, description="Detected shop name if found")
    date_detected: Optional[str] = Field(None, description="Detected date if found")
    processing_time_ms: int = Field(..., description="Processing time in milliseconds")
    error_message: Optional[str] = Field(None, description="Error details if failed")
    
    class Config:
        json_schema_extra = {
            "example": {
                "status": "success",
                "raw_lines": [
                    {"text": "SUPERINDO", "confidence": 0.98, "bounding_box": {"x1": 100, "y1": 10, "x2": 250, "y2": 40}}
                ],
                "receipt_lines": [
                    {"raw_text": "TEPUNG TERIGU 1KG 18.500", "item_name": "TEPUNG TERIGU", "quantity": 1, "unit": "KG", "price": 18500, "confidence": 0.92}
                ],
                "total_detected": 105000,
                "shop_name": "SUPERINDO",
                "processing_time_ms": 1250
            }
        }


class HealthResponse(BaseModel):
    """Health check response."""
    status: str = "healthy"
    ocr_loaded: bool = False
    version: str = "1.0.0"
```

### PaddleOCR Wrapper:

Create `ocr-service/app/ocr.py`:

```python
"""
PaddleOCR wrapper with lazy loading and caching.
"""
import logging
from typing import List, Tuple, Optional
from functools import lru_cache
import numpy as np
from PIL import Image
import io
import base64

logger = logging.getLogger(__name__)

# Global OCR instance (lazy loaded)
_ocr_instance = None


def get_ocr():
    """
    Lazy load PaddleOCR instance.
    This is expensive, so we cache it globally.
    """
    global _ocr_instance
    
    if _ocr_instance is None:
        logger.info("Loading PaddleOCR model (this may take a moment)...")
        
        from paddleocr import PaddleOCR
        
        # Initialize with Indonesian support
        # use_angle_cls=True enables text angle detection
        # use_gpu=False for CPU (set True if GPU available)
        _ocr_instance = PaddleOCR(
            use_angle_cls=True,
            lang='id',  # Indonesian
            use_gpu=False,
            show_log=False,
            det_db_thresh=0.3,
            det_db_box_thresh=0.5,
            drop_score=0.5
        )
        
        logger.info("PaddleOCR model loaded successfully")
    
    return _ocr_instance


def decode_base64_image(base64_string: str) -> np.ndarray:
    """
    Decode base64 string to numpy array for OCR.
    """
    # Remove data URL prefix if present
    if ',' in base64_string:
        base64_string = base64_string.split(',')[1]
    
    # Decode base64
    image_bytes = base64.b64decode(base64_string)
    
    # Open with PIL and convert to numpy
    image = Image.open(io.BytesIO(image_bytes))
    
    # Convert to RGB if needed
    if image.mode != 'RGB':
        image = image.convert('RGB')
    
    return np.array(image)


def run_ocr(image: np.ndarray) -> List[Tuple[List[List[int]], Tuple[str, float]]]:
    """
    Run OCR on an image array.
    
    Returns:
        List of (bounding_box, (text, confidence)) tuples
    """
    ocr = get_ocr()
    
    # Run OCR
    result = ocr.ocr(image, cls=True)
    
    # Handle empty results
    if result is None or len(result) == 0 or result[0] is None:
        return []
    
    return result[0]


def extract_text_lines(
    ocr_result: List[Tuple[List[List[int]], Tuple[str, float]]]
) -> List[dict]:
    """
    Convert PaddleOCR result to structured text lines.
    """
    lines = []
    
    for item in ocr_result:
        if item is None or len(item) < 2:
            continue
            
        bbox, (text, confidence) = item
        
        # Get bounding box corners
        x_coords = [p[0] for p in bbox]
        y_coords = [p[1] for p in bbox]
        
        lines.append({
            "text": text.strip(),
            "confidence": float(confidence),
            "bounding_box": {
                "x1": int(min(x_coords)),
                "y1": int(min(y_coords)),
                "x2": int(max(x_coords)),
                "y2": int(max(y_coords))
            }
        })
    
    # Sort by Y coordinate (top to bottom)
    lines.sort(key=lambda x: x["bounding_box"]["y1"])
    
    return lines


def is_ocr_loaded() -> bool:
    """Check if OCR model is loaded."""
    return _ocr_instance is not None
```

### Your Task (Day 1):

1. Create the `ocr-service/` folder structure
2. Create `requirements.txt` with dependencies
3. Create `models.py` with Pydantic schemas
4. Create `ocr.py` with PaddleOCR wrapper
5. Test Python imports: `pip install -r requirements.txt`

---

# Day 2: FastAPI Receipt Scanning Endpoint

## 🧒 Explain Like I'm 5

Now we need to create a "door" 🚪 where our main app can knock and say:
- "Hey OCR service! Here's a picture of a receipt!"
- The OCR service reads it and replies: "Here's what I found!"

FastAPI is like a super-fast butler who answers the door and handles requests!

## 🔧 Engineer Language

**FastAPI** provides high-performance async endpoints with automatic OpenAPI documentation. We create a `/scan` endpoint that accepts images and returns structured OCR results.

> 📖 **Microsoft Docs**: *"Use HttpClient to make HTTP calls to external services. Configure retry policies and circuit breakers for resilience."*
>
> — [Make HTTP requests with IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)

### Receipt Parsing Logic:

Create `ocr-service/app/preprocessing.py`:

```python
"""
Receipt text preprocessing and parsing.
"""
import re
from typing import List, Optional, Tuple
from .models import ReceiptLine, TextLine

# Common Indonesian receipt patterns
PRICE_PATTERN = re.compile(r'[\d,.]+(?:\s*[.,]\s*\d{2,3})?$')
QUANTITY_PATTERN = re.compile(r'^(\d+(?:[.,]\d+)?)\s*(kg|g|pcs|btr|butir|ltr|ml|pack|dus|box)?', re.IGNORECASE)
TOTAL_KEYWORDS = ['total', 'subtotal', 'jumlah', 'grand total', 'tunai', 'cash']
SKIP_KEYWORDS = ['terima kasih', 'thank you', 'struk', 'receipt', 'kasir', 'cashier', 'tanggal', 'date']


def clean_text(text: str) -> str:
    """Clean OCR text for parsing."""
    # Remove extra whitespace
    text = ' '.join(text.split())
    # Remove common OCR artifacts
    text = text.replace('|', 'I').replace('0', 'O').replace('1', 'I')
    return text.strip()


def extract_price(text: str) -> Optional[float]:
    """
    Extract price from text line.
    Handles formats: 25000, 25.000, 25,000, Rp 25.000
    """
    # Remove currency symbols and whitespace
    cleaned = re.sub(r'[Rp\s]', '', text)
    
    # Find number at end of line (usually the price)
    match = PRICE_PATTERN.search(cleaned)
    if match:
        price_str = match.group()
        # Normalize decimal separators
        # Indonesian uses . as thousand separator
        price_str = price_str.replace('.', '').replace(',', '.')
        try:
            return float(price_str)
        except ValueError:
            pass
    
    return None


def extract_quantity_and_unit(text: str) -> Tuple[Optional[float], Optional[str]]:
    """
    Extract quantity and unit from text.
    E.g., "2 kg" -> (2.0, "kg"), "500g" -> (500.0, "g")
    """
    match = QUANTITY_PATTERN.search(text)
    if match:
        qty_str = match.group(1).replace(',', '.')
        unit = match.group(2) if match.group(2) else None
        try:
            return float(qty_str), unit
        except ValueError:
            pass
    
    return None, None


def is_item_line(text: str) -> bool:
    """Check if line appears to be a receipt item (not header/footer)."""
    text_lower = text.lower()
    
    # Skip common non-item lines
    for keyword in SKIP_KEYWORDS:
        if keyword in text_lower:
            return False
    
    # Must have some letters (item name) and numbers (price/qty)
    has_letters = bool(re.search(r'[a-zA-Z]', text))
    has_numbers = bool(re.search(r'\d', text))
    
    return has_letters and has_numbers


def is_total_line(text: str) -> bool:
    """Check if line is a total line."""
    text_lower = text.lower()
    return any(keyword in text_lower for keyword in TOTAL_KEYWORDS)


def parse_receipt_line(text_line: TextLine) -> ReceiptLine:
    """
    Parse a single OCR text line into a structured receipt line.
    """
    text = text_line.text
    price = extract_price(text)
    quantity, unit = extract_quantity_and_unit(text)
    
    # Extract item name (text before the price/quantity)
    item_name = text
    if price:
        # Remove price from end
        item_name = PRICE_PATTERN.sub('', item_name)
    if quantity:
        # Remove quantity from beginning
        item_name = QUANTITY_PATTERN.sub('', item_name)
    
    # Clean up item name
    item_name = clean_text(item_name)
    item_name = re.sub(r'[^\w\s]', '', item_name).strip()
    
    return ReceiptLine(
        raw_text=text,
        item_name=item_name if item_name else None,
        quantity=quantity,
        unit=unit.upper() if unit else None,
        price=price,
        confidence=text_line.confidence
    )


def parse_receipt(text_lines: List[TextLine]) -> dict:
    """
    Parse all OCR lines into structured receipt data.
    
    Returns:
        dict with receipt_lines, total_detected, shop_name, date_detected
    """
    receipt_lines = []
    total_detected = None
    shop_name = None
    date_detected = None
    
    for i, line in enumerate(text_lines):
        text = line.text
        
        # First line with high confidence is often shop name
        if i == 0 and line.confidence > 0.8:
            # Check if it looks like a shop name (not a date/time)
            if not re.search(r'\d{2}[/-]\d{2}', text):
                shop_name = text
                continue
        
        # Check for date
        date_match = re.search(r'(\d{2}[/-]\d{2}[/-]\d{2,4})', text)
        if date_match and not date_detected:
            date_detected = date_match.group(1)
            continue
        
        # Check for total
        if is_total_line(text):
            total = extract_price(text)
            if total and total > 1000:  # Reasonable total amount
                total_detected = total
            continue
        
        # Parse as item line
        if is_item_line(text):
            receipt_line = parse_receipt_line(line)
            if receipt_line.price or receipt_line.item_name:
                receipt_lines.append(receipt_line)
    
    return {
        "receipt_lines": receipt_lines,
        "total_detected": total_detected,
        "shop_name": shop_name,
        "date_detected": date_detected
    }
```

### FastAPI Application:

Create `ocr-service/app/main.py`:

```python
"""
FastAPI application for PaddleOCR receipt scanning.
"""
import logging
import time
from contextlib import asynccontextmanager
from typing import Optional

from fastapi import FastAPI, HTTPException, UploadFile, File, Form
from fastapi.middleware.cors import CORSMiddleware
import httpx
import numpy as np
from PIL import Image
import io

from .models import OcrRequest, OcrResponse, OcrStatus, HealthResponse, TextLine
from .ocr import get_ocr, decode_base64_image, run_ocr, extract_text_lines, is_ocr_loaded
from .preprocessing import parse_receipt

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Lifecycle manager - preload OCR model on startup.
    """
    logger.info("Starting OCR service...")
    
    # Preload OCR model (optional - can also lazy load)
    try:
        get_ocr()
        logger.info("OCR model preloaded successfully")
    except Exception as e:
        logger.warning(f"Failed to preload OCR model: {e}")
    
    yield
    
    logger.info("Shutting down OCR service...")


# Create FastAPI app
app = FastAPI(
    title="Nastart OCR Service",
    description="PaddleOCR-based receipt scanning microservice",
    version="1.0.0",
    lifespan=lifespan
)

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health", response_model=HealthResponse, tags=["Health"])
async def health_check():
    """
    Health check endpoint.
    Returns OCR model load status.
    """
    return HealthResponse(
        status="healthy",
        ocr_loaded=is_ocr_loaded(),
        version="1.0.0"
    )


@app.post("/scan", response_model=OcrResponse, tags=["OCR"])
async def scan_receipt(request: OcrRequest):
    """
    Scan a receipt image and extract text.
    
    Accepts either:
    - `image_base64`: Base64 encoded image
    - `image_url`: URL to fetch image from
    """
    start_time = time.time()
    
    try:
        # Get image data
        if request.image_base64:
            image = decode_base64_image(request.image_base64)
        elif request.image_url:
            # Fetch image from URL
            async with httpx.AsyncClient() as client:
                response = await client.get(request.image_url, timeout=30.0)
                response.raise_for_status()
                image = np.array(Image.open(io.BytesIO(response.content)).convert('RGB'))
        else:
            raise HTTPException(
                status_code=400,
                detail="Either image_base64 or image_url must be provided"
            )
        
        # Run OCR
        ocr_result = run_ocr(image)
        
        if not ocr_result:
            return OcrResponse(
                status=OcrStatus.FAILED,
                raw_lines=[],
                receipt_lines=[],
                processing_time_ms=int((time.time() - start_time) * 1000),
                error_message="No text detected in image"
            )
        
        # Extract text lines
        text_lines = extract_text_lines(ocr_result)
        raw_lines = [TextLine(**line) for line in text_lines]
        
        # Parse receipt
        parsed = parse_receipt(raw_lines)
        
        processing_time = int((time.time() - start_time) * 1000)
        
        return OcrResponse(
            status=OcrStatus.SUCCESS if parsed["receipt_lines"] else OcrStatus.PARTIAL,
            raw_lines=raw_lines,
            receipt_lines=parsed["receipt_lines"],
            total_detected=parsed["total_detected"],
            shop_name=parsed["shop_name"],
            date_detected=parsed["date_detected"],
            processing_time_ms=processing_time
        )
        
    except HTTPException:
        raise
    except Exception as e:
        logger.exception("OCR processing failed")
        return OcrResponse(
            status=OcrStatus.FAILED,
            raw_lines=[],
            receipt_lines=[],
            processing_time_ms=int((time.time() - start_time) * 1000),
            error_message=str(e)
        )


@app.post("/scan/upload", response_model=OcrResponse, tags=["OCR"])
async def scan_receipt_upload(
    file: UploadFile = File(..., description="Receipt image file"),
    language: str = Form(default="id", description="OCR language")
):
    """
    Scan a receipt image uploaded as multipart form.
    """
    start_time = time.time()
    
    try:
        # Read and validate file
        contents = await file.read()
        
        if not contents:
            raise HTTPException(status_code=400, detail="Empty file")
        
        # Validate file type
        content_type = file.content_type or ""
        if not content_type.startswith("image/"):
            raise HTTPException(
                status_code=400,
                detail=f"Invalid file type: {content_type}. Must be an image."
            )
        
        # Convert to numpy array
        image = np.array(Image.open(io.BytesIO(contents)).convert('RGB'))
        
        # Run OCR
        ocr_result = run_ocr(image)
        
        if not ocr_result:
            return OcrResponse(
                status=OcrStatus.FAILED,
                raw_lines=[],
                receipt_lines=[],
                processing_time_ms=int((time.time() - start_time) * 1000),
                error_message="No text detected in image"
            )
        
        # Extract and parse
        text_lines = extract_text_lines(ocr_result)
        raw_lines = [TextLine(**line) for line in text_lines]
        parsed = parse_receipt(raw_lines)
        
        return OcrResponse(
            status=OcrStatus.SUCCESS if parsed["receipt_lines"] else OcrStatus.PARTIAL,
            raw_lines=raw_lines,
            receipt_lines=parsed["receipt_lines"],
            total_detected=parsed["total_detected"],
            shop_name=parsed["shop_name"],
            date_detected=parsed["date_detected"],
            processing_time_ms=int((time.time() - start_time) * 1000)
        )
        
    except HTTPException:
        raise
    except Exception as e:
        logger.exception("OCR upload processing failed")
        return OcrResponse(
            status=OcrStatus.FAILED,
            raw_lines=[],
            receipt_lines=[],
            processing_time_ms=int((time.time() - start_time) * 1000),
            error_message=str(e)
        )


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8001)
```

### Dockerfile:

Create `ocr-service/Dockerfile`:

```dockerfile
FROM python:3.11-slim

WORKDIR /app

# Install system dependencies for PaddleOCR
RUN apt-get update && apt-get install -y \
    libgl1-mesa-glx \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements first for caching
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY app/ ./app/

# Expose port
EXPOSE 8001

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD python -c "import httpx; httpx.get('http://localhost:8001/health').raise_for_status()"

# Run the application
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8001"]
```

### Update docker-compose.yml:

Add to your `docker-compose.yml`:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: nastart
      POSTGRES_PASSWORD: nastart123
      POSTGRES_DB: nastart
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U nastart"]
      interval: 10s
      timeout: 5s
      retries: 5

  ocr-service:
    build:
      context: ./ocr-service
      dockerfile: Dockerfile
    ports:
      - "8001:8001"
    environment:
      - PYTHONUNBUFFERED=1
    healthcheck:
      test: ["CMD", "python", "-c", "import httpx; httpx.get('http://localhost:8001/health').raise_for_status()"]
      interval: 30s
      timeout: 10s
      start_period: 60s
      retries: 3
    deploy:
      resources:
        limits:
          memory: 2G  # PaddleOCR needs memory

  api:
    build:
      context: ./backend
      dockerfile: src/Nastart.Api/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=nastart;Username=nastart;Password=nastart123
      - OcrService__BaseUrl=http://ocr-service:8001
    depends_on:
      postgres:
        condition: service_healthy
      ocr-service:
        condition: service_healthy

volumes:
  postgres_data:
```

### Your Task (Day 2):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart

# Create FastAPI main file
# (already done above)

# Test locally (optional)
cd ocr-service
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8001

# Or build with Docker
docker compose build ocr-service
docker compose up ocr-service
```

---

# Day 3: PaddleOcrService HTTP Client

## 🧒 Explain Like I'm 5

Now our main .NET app needs to "talk" to the OCR service. It's like calling a friend on the phone:
1. "Hey OCR, I'm sending you a picture!"
2. OCR: "Got it! Here's what I read..."
3. "Thanks! Now I can save this to my inventory!"

**HttpClient** is the telephone that makes these calls!

## 🔧 Engineer Language

We create a typed HTTP client using `IHttpClientFactory` — the recommended pattern for .NET HTTP calls with automatic connection management, retry policies, and DI integration.

> 📖 **Microsoft Docs**: *"IHttpClientFactory manages the pooling and lifetime of underlying HttpClientHandler instances. It provides factory methods for configuring named and typed clients."*
>
> — [IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory)

### OCR Service Client:

Create `src/Nastart.Api/Shared/Services/OcrService/IPaddleOcrService.cs`:

```csharp
namespace Nastart.Api.Shared.Services.OcrService;

/// <summary>
/// Interface for PaddleOCR service client.
/// </summary>
public interface IPaddleOcrService
{
    /// <summary>
    /// Scans a receipt image and extracts text.
    /// </summary>
    /// <param name="imageBase64">Base64 encoded image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OCR scan result</returns>
    Task<OcrScanResult> ScanReceiptAsync(
        string imageBase64, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Scans a receipt from a URL.
    /// </summary>
    /// <param name="imageUrl">URL of the image</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<OcrScanResult> ScanReceiptFromUrlAsync(
        string imageUrl, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if OCR service is healthy.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
```

### OCR DTOs:

Create `src/Nastart.Api/Shared/Services/OcrService/OcrModels.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Nastart.Api.Shared.Services.OcrService;

/// <summary>
/// Result from OCR scanning.
/// </summary>
public sealed record OcrScanResult
{
    public required OcrStatus Status { get; init; }
    public IReadOnlyList<OcrTextLine> RawLines { get; init; } = [];
    public IReadOnlyList<OcrReceiptLine> ReceiptLines { get; init; } = [];
    public decimal? TotalDetected { get; init; }
    public string? ShopName { get; init; }
    public string? DateDetected { get; init; }
    public int ProcessingTimeMs { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// OCR processing status.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OcrStatus
{
    Success,
    Partial,
    Failed
}

/// <summary>
/// A single line of detected text.
/// </summary>
public sealed record OcrTextLine
{
    public required string Text { get; init; }
    public required decimal Confidence { get; init; }
    public OcrBoundingBox? BoundingBox { get; init; }
}

/// <summary>
/// Bounding box for detected text.
/// </summary>
public sealed record OcrBoundingBox(int X1, int Y1, int X2, int Y2);

/// <summary>
/// A parsed receipt line item.
/// </summary>
public sealed record OcrReceiptLine
{
    public required string RawText { get; init; }
    public string? ItemName { get; init; }
    public decimal? Quantity { get; init; }
    public string? Unit { get; init; }
    public decimal? Price { get; init; }
    public decimal Confidence { get; init; }
}

/// <summary>
/// Request to OCR service.
/// </summary>
internal sealed record OcrScanRequest
{
    [JsonPropertyName("image_base64")]
    public string? ImageBase64 { get; init; }
    
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; init; }
    
    [JsonPropertyName("language")]
    public string Language { get; init; } = "id";
}

/// <summary>
/// Response from OCR service.
/// </summary>
internal sealed record OcrScanResponse
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
    
    [JsonPropertyName("raw_lines")]
    public List<OcrTextLineResponse> RawLines { get; init; } = [];
    
    [JsonPropertyName("receipt_lines")]
    public List<OcrReceiptLineResponse> ReceiptLines { get; init; } = [];
    
    [JsonPropertyName("total_detected")]
    public decimal? TotalDetected { get; init; }
    
    [JsonPropertyName("shop_name")]
    public string? ShopName { get; init; }
    
    [JsonPropertyName("date_detected")]
    public string? DateDetected { get; init; }
    
    [JsonPropertyName("processing_time_ms")]
    public int ProcessingTimeMs { get; init; }
    
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }
}

internal sealed record OcrTextLineResponse
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
    
    [JsonPropertyName("confidence")]
    public decimal Confidence { get; init; }
    
    [JsonPropertyName("bounding_box")]
    public OcrBoundingBoxResponse? BoundingBox { get; init; }
}

internal sealed record OcrBoundingBoxResponse
{
    [JsonPropertyName("x1")]
    public int X1 { get; init; }
    
    [JsonPropertyName("y1")]
    public int Y1 { get; init; }
    
    [JsonPropertyName("x2")]
    public int X2 { get; init; }
    
    [JsonPropertyName("y2")]
    public int Y2 { get; init; }
}

internal sealed record OcrReceiptLineResponse
{
    [JsonPropertyName("raw_text")]
    public string RawText { get; init; } = "";
    
    [JsonPropertyName("item_name")]
    public string? ItemName { get; init; }
    
    [JsonPropertyName("quantity")]
    public decimal? Quantity { get; init; }
    
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }
    
    [JsonPropertyName("price")]
    public decimal? Price { get; init; }
    
    [JsonPropertyName("confidence")]
    public decimal Confidence { get; init; }
}
```

### HTTP Client Implementation:

Create `src/Nastart.Api/Shared/Services/OcrService/PaddleOcrService.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Nastart.Api.Shared.Services.OcrService;

/// <summary>
/// HTTP client for PaddleOCR microservice.
/// </summary>
/// <remarks>
/// Uses typed HttpClient pattern for clean DI and configuration.
/// See: https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory
/// </remarks>
public class PaddleOcrService : IPaddleOcrService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaddleOcrService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public PaddleOcrService(
        HttpClient httpClient,
        ILogger<PaddleOcrService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<OcrScanResult> ScanReceiptAsync(
        string imageBase64, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending receipt to OCR service");
        
        var request = new OcrScanRequest
        {
            ImageBase64 = imageBase64,
            Language = "id"
        };
        
        return await SendScanRequestAsync(request, cancellationToken);
    }

    public async Task<OcrScanResult> ScanReceiptFromUrlAsync(
        string imageUrl, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending receipt URL to OCR service: {Url}", imageUrl);
        
        var request = new OcrScanRequest
        {
            ImageUrl = imageUrl,
            Language = "id"
        };
        
        return await SendScanRequestAsync(request, cancellationToken);
    }

    private async Task<OcrScanResult> SendScanRequestAsync(
        OcrScanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/scan", 
                request, 
                _jsonOptions,
                cancellationToken);
            
            response.EnsureSuccessStatusCode();
            
            var ocrResponse = await response.Content
                .ReadFromJsonAsync<OcrScanResponse>(_jsonOptions, cancellationToken);
            
            if (ocrResponse is null)
            {
                return CreateFailedResult("Empty response from OCR service");
            }
            
            return MapToResult(ocrResponse);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OCR service request failed");
            return CreateFailedResult($"OCR service unavailable: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "OCR service request timed out");
            return CreateFailedResult("OCR service timed out");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse OCR response");
            return CreateFailedResult($"Invalid OCR response: {ex.Message}");
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static OcrScanResult MapToResult(OcrScanResponse response)
    {
        var status = response.Status.ToLowerInvariant() switch
        {
            "success" => OcrStatus.Success,
            "partial" => OcrStatus.Partial,
            _ => OcrStatus.Failed
        };
        
        return new OcrScanResult
        {
            Status = status,
            RawLines = response.RawLines.Select(l => new OcrTextLine
            {
                Text = l.Text,
                Confidence = l.Confidence,
                BoundingBox = l.BoundingBox is not null 
                    ? new OcrBoundingBox(l.BoundingBox.X1, l.BoundingBox.Y1, 
                        l.BoundingBox.X2, l.BoundingBox.Y2)
                    : null
            }).ToList(),
            ReceiptLines = response.ReceiptLines.Select(l => new OcrReceiptLine
            {
                RawText = l.RawText,
                ItemName = l.ItemName,
                Quantity = l.Quantity,
                Unit = l.Unit,
                Price = l.Price,
                Confidence = l.Confidence
            }).ToList(),
            TotalDetected = response.TotalDetected,
            ShopName = response.ShopName,
            DateDetected = response.DateDetected,
            ProcessingTimeMs = response.ProcessingTimeMs,
            ErrorMessage = response.ErrorMessage
        };
    }

    private static OcrScanResult CreateFailedResult(string error) => new()
    {
        Status = OcrStatus.Failed,
        ErrorMessage = error,
        ProcessingTimeMs = 0
    };
}
```

### Configuration:

Create `src/Nastart.Api/Shared/Services/OcrService/OcrServiceOptions.cs`:

```csharp
namespace Nastart.Api.Shared.Services.OcrService;

/// <summary>
/// Configuration options for OCR service.
/// </summary>
public class OcrServiceOptions
{
    public const string SectionName = "OcrService";
    
    /// <summary>
    /// Base URL of the OCR service.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8001";
    
    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
    
    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; } = 3;
}
```

### Register in Program.cs:

```csharp
using Nastart.Api.Shared.Services.OcrService;

var builder = WebApplication.CreateBuilder(args);

// Configure OCR service
builder.Services.Configure<OcrServiceOptions>(
    builder.Configuration.GetSection(OcrServiceOptions.SectionName));

// Add typed HttpClient for OCR service
builder.Services.AddHttpClient<IPaddleOcrService, PaddleOcrService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<OcrServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Allow self-signed certs in development
    ServerCertificateCustomValidationCallback = 
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

// ... rest of configuration
```

### Add to appsettings.json:

```json
{
  "OcrService": {
    "BaseUrl": "http://localhost:8001",
    "TimeoutSeconds": 60,
    "RetryCount": 3
  }
}
```

### Your Task (Day 3):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create OcrService folder
mkdir Shared\Services\OcrService

# Create service files
New-Item Shared\Services\OcrService\IPaddleOcrService.cs
New-Item Shared\Services\OcrService\PaddleOcrService.cs
New-Item Shared\Services\OcrService\OcrModels.cs
New-Item Shared\Services\OcrService\OcrServiceOptions.cs

# Verify build
dotnet build
```

---

# Day 4: ScanReceipt Feature Slice

## 🧒 Explain Like I'm 5

Now we connect everything! When a baker sends a receipt photo:
1. We send it to the OCR service
2. OCR reads the text
3. We find matching ingredients in our database
4. We show the baker what we found
5. They confirm, and we save it!

## 🔧 Engineer Language

The **ScanReceipt** feature slice orchestrates the receipt scanning workflow. It calls the OCR service, matches detected items to ingredients, and returns results for user confirmation.

> 📖 **Microsoft Docs**: *"Minimal APIs use MapPost, MapGet, etc. to define endpoints inline with automatic parameter binding."*
>
> — [Minimal APIs overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview)

### ScanReceipt Feature:

Create `src/Nastart.Api/Features/Purchases/ScanReceipt.cs`:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;
using Nastart.Api.Shared.Services.OcrService;

namespace Nastart.Api.Features.Purchases;

// ══════════════════════════════════════════════════════════════
// SCAN RECEIPT FEATURE SLICE
// Handles OCR scanning and ingredient matching
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to scan a receipt image.
/// </summary>
public sealed record ScanReceiptCommand(
    Guid UserId,
    string ImageBase64,
    string? ShopName = null
) : IRequest<Result<ScanReceiptResponse>>;

/// <summary>
/// Alternative: scan from URL (for Telegram file URLs).
/// </summary>
public sealed record ScanReceiptFromUrlCommand(
    Guid UserId,
    string ImageUrl,
    string? ShopName = null
) : IRequest<Result<ScanReceiptResponse>>;

// ── Response ──
/// <summary>
/// Response with scanned items for user confirmation.
/// </summary>
public sealed record ScanReceiptResponse(
    Guid ScanId,
    string? DetectedShopName,
    string? DetectedDate,
    decimal? DetectedTotal,
    IReadOnlyList<ScannedItemResponse> Items,
    IReadOnlyList<string> UnmatchedLines,
    int ProcessingTimeMs,
    ScanStatus Status
);

/// <summary>
/// A scanned item with match suggestion.
/// </summary>
public sealed record ScannedItemResponse(
    string RawText,
    string? DetectedName,
    decimal? DetectedQuantity,
    string? DetectedUnit,
    decimal? DetectedPrice,
    decimal Confidence,
    IngredientMatchResponse? SuggestedMatch
);

/// <summary>
/// Suggested ingredient match.
/// </summary>
public sealed record IngredientMatchResponse(
    Guid IngredientId,
    string IngredientName,
    string Unit,
    decimal CurrentPrice,
    decimal MatchScore
);

/// <summary>
/// Scan processing status.
/// </summary>
public enum ScanStatus
{
    Success,
    PartialSuccess,
    NoItemsFound,
    OcrFailed
}

// ── Validator ──
public sealed class ScanReceiptValidator : AbstractValidator<ScanReceiptCommand>
{
    public ScanReceiptValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
        
        RuleFor(x => x.ImageBase64)
            .NotEmpty()
            .WithMessage("Image data is required")
            .Must(BeValidBase64)
            .WithMessage("Invalid base64 image data");
    }
    
    private static bool BeValidBase64(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return false;
        
        // Remove data URL prefix if present
        var data = base64.Contains(',') 
            ? base64.Split(',')[1] 
            : base64;
        
        try
        {
            Convert.FromBase64String(data);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

// ── Handler ──
/// <summary>
/// Handles receipt scanning via OCR service.
/// </summary>
public sealed class ScanReceiptHandler 
    : IRequestHandler<ScanReceiptCommand, Result<ScanReceiptResponse>>
{
    private readonly IPaddleOcrService _ocrService;
    private readonly NastartDbContext _db;
    private readonly ILogger<ScanReceiptHandler> _logger;

    public ScanReceiptHandler(
        IPaddleOcrService ocrService,
        NastartDbContext db,
        ILogger<ScanReceiptHandler> logger)
    {
        _ocrService = ocrService;
        _db = db;
        _logger = logger;
    }

    public async Task<Result<ScanReceiptResponse>> Handle(
        ScanReceiptCommand request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Scanning receipt for user {UserId}", 
            request.UserId);
        
        // Call OCR service
        var ocrResult = await _ocrService.ScanReceiptAsync(
            request.ImageBase64, 
            cancellationToken);
        
        if (ocrResult.Status == OcrStatus.Failed)
        {
            _logger.LogWarning(
                "OCR failed: {Error}", 
                ocrResult.ErrorMessage);
            
            return Result<ScanReceiptResponse>.Success(new ScanReceiptResponse(
                ScanId: Guid.NewGuid(),
                DetectedShopName: null,
                DetectedDate: null,
                DetectedTotal: null,
                Items: [],
                UnmatchedLines: [],
                ProcessingTimeMs: ocrResult.ProcessingTimeMs,
                Status: ScanStatus.OcrFailed
            ));
        }
        
        // Get user's ingredients for matching
        var userIngredients = await _db.Ingredients
            .Where(i => i.UserId == request.UserId)
            .Select(i => new 
            { 
                i.Id, 
                i.Name, 
                i.Unit, 
                i.CurrentPrice,
                NameLower = i.Name.ToLower()
            })
            .ToListAsync(cancellationToken);
        
        // Match OCR lines to ingredients
        var scannedItems = new List<ScannedItemResponse>();
        var unmatchedLines = new List<string>();
        
        foreach (var line in ocrResult.ReceiptLines)
        {
            var match = FindBestMatch(line.ItemName, userIngredients);
            
            scannedItems.Add(new ScannedItemResponse(
                RawText: line.RawText,
                DetectedName: line.ItemName,
                DetectedQuantity: line.Quantity,
                DetectedUnit: line.Unit,
                DetectedPrice: line.Price,
                Confidence: line.Confidence,
                SuggestedMatch: match
            ));
            
            if (match is null && !string.IsNullOrWhiteSpace(line.RawText))
            {
                unmatchedLines.Add(line.RawText);
            }
        }
        
        // Determine status
        var status = scannedItems.Count switch
        {
            0 => ScanStatus.NoItemsFound,
            _ when scannedItems.All(i => i.SuggestedMatch is not null) => ScanStatus.Success,
            _ => ScanStatus.PartialSuccess
        };
        
        _logger.LogInformation(
            "Scanned receipt: {ItemCount} items, {MatchedCount} matched",
            scannedItems.Count,
            scannedItems.Count(i => i.SuggestedMatch is not null));
        
        return Result<ScanReceiptResponse>.Success(new ScanReceiptResponse(
            ScanId: Guid.NewGuid(),
            DetectedShopName: request.ShopName ?? ocrResult.ShopName,
            DetectedDate: ocrResult.DateDetected,
            DetectedTotal: ocrResult.TotalDetected,
            Items: scannedItems,
            UnmatchedLines: unmatchedLines,
            ProcessingTimeMs: ocrResult.ProcessingTimeMs,
            Status: status
        ));
    }
    
    /// <summary>
    /// Find best matching ingredient using fuzzy matching.
    /// </summary>
    private IngredientMatchResponse? FindBestMatch(
        string? itemName,
        List<dynamic> ingredients)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return null;
        
        var searchTerm = itemName.ToLower();
        
        // Simple matching - will be improved with FuzzyMatchingService
        var bestMatch = ingredients
            .Select(i => new
            {
                Ingredient = i,
                Score = CalculateMatchScore(searchTerm, (string)i.NameLower)
            })
            .Where(m => m.Score > 0.5m) // Minimum 50% match
            .OrderByDescending(m => m.Score)
            .FirstOrDefault();
        
        if (bestMatch is null)
            return null;
        
        return new IngredientMatchResponse(
            IngredientId: bestMatch.Ingredient.Id,
            IngredientName: bestMatch.Ingredient.Name,
            Unit: bestMatch.Ingredient.Unit,
            CurrentPrice: bestMatch.Ingredient.CurrentPrice,
            MatchScore: bestMatch.Score
        );
    }
    
    /// <summary>
    /// Simple string similarity score (0-1).
    /// </summary>
    private static decimal CalculateMatchScore(string search, string target)
    {
        // Exact match
        if (search == target)
            return 1.0m;
        
        // Contains match
        if (target.Contains(search) || search.Contains(target))
            return 0.8m;
        
        // Word match
        var searchWords = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var targetWords = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var matchingWords = searchWords.Count(sw => 
            targetWords.Any(tw => tw.Contains(sw) || sw.Contains(tw)));
        
        if (matchingWords > 0)
            return (decimal)matchingWords / Math.Max(searchWords.Length, targetWords.Length);
        
        return 0m;
    }
}

// ── URL Handler ──
/// <summary>
/// Handles receipt scanning from URL.
/// </summary>
public sealed class ScanReceiptFromUrlHandler 
    : IRequestHandler<ScanReceiptFromUrlCommand, Result<ScanReceiptResponse>>
{
    private readonly IPaddleOcrService _ocrService;
    private readonly IMediator _mediator;
    private readonly ILogger<ScanReceiptFromUrlHandler> _logger;
    private readonly HttpClient _httpClient;

    public ScanReceiptFromUrlHandler(
        IPaddleOcrService ocrService,
        IMediator mediator,
        ILogger<ScanReceiptFromUrlHandler> logger,
        IHttpClientFactory httpClientFactory)
    {
        _ocrService = ocrService;
        _mediator = mediator;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<Result<ScanReceiptResponse>> Handle(
        ScanReceiptFromUrlCommand request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Downloading receipt from URL for user {UserId}", 
            request.UserId);
        
        try
        {
            // Download image
            var imageBytes = await _httpClient.GetByteArrayAsync(
                request.ImageUrl, 
                cancellationToken);
            
            var imageBase64 = Convert.ToBase64String(imageBytes);
            
            // Delegate to base handler
            return await _mediator.Send(new ScanReceiptCommand(
                UserId: request.UserId,
                ImageBase64: imageBase64,
                ShopName: request.ShopName
            ), cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to download image from {Url}", request.ImageUrl);
            
            return Result<ScanReceiptResponse>.Failure(
                Error.Validation("IMAGE_DOWNLOAD_FAILED", 
                    $"Failed to download image: {ex.Message}"));
        }
    }
}
```

### Your Task (Day 4):

1. Create `ScanReceipt.cs` in `Features/Purchases/`
2. Register `IPaddleOcrService` in `Program.cs`
3. Verify build: `dotnet build`

---

# Day 5: FuzzyMatchingService for Ingredients

## 🧒 Explain Like I'm 5

OCR might read "TEPUNG TRGU" instead of "Tepung Terigu" (it made a tiny mistake!). We need a smart matcher that says:
- "Hmm, TEPUNG TRGU looks a LOT like Tepung Terigu..."
- "They're probably the same thing!"

It's like recognizing your friend even if they're wearing a funny hat! 🎩

## 🔧 Engineer Language

**Fuzzy matching** finds similar strings using algorithms like Levenshtein distance, Jaro-Winkler, or token-based matching. This handles OCR errors and variations in ingredient names.

> 📖 **Microsoft Docs**: *"String comparison and sorting in .NET can be culture-sensitive. Use StringComparison.OrdinalIgnoreCase for culture-independent comparisons."*
>
> — [Best practices for strings](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings)

### FuzzyMatchingService:

Create `src/Nastart.Api/Shared/Services/Matching/IFuzzyMatchingService.cs`:

```csharp
namespace Nastart.Api.Shared.Services.Matching;

/// <summary>
/// Service for fuzzy string matching.
/// </summary>
public interface IFuzzyMatchingService
{
    /// <summary>
    /// Finds the best match for a search term from a list of candidates.
    /// </summary>
    MatchResult? FindBestMatch(string searchTerm, IEnumerable<MatchCandidate> candidates);
    
    /// <summary>
    /// Finds all matches above a threshold.
    /// </summary>
    IReadOnlyList<MatchResult> FindAllMatches(
        string searchTerm, 
        IEnumerable<MatchCandidate> candidates,
        decimal minScore = 0.5m);
    
    /// <summary>
    /// Calculates similarity score between two strings (0-1).
    /// </summary>
    decimal CalculateSimilarity(string a, string b);
}

/// <summary>
/// A candidate for matching.
/// </summary>
public sealed record MatchCandidate(
    string Id,
    string Text,
    string[]? Aliases = null
);

/// <summary>
/// Result of a fuzzy match.
/// </summary>
public sealed record MatchResult(
    string CandidateId,
    string CandidateText,
    decimal Score,
    string MatchType
);
```

### Implementation:

Create `src/Nastart.Api/Shared/Services/Matching/FuzzyMatchingService.cs`:

```csharp
namespace Nastart.Api.Shared.Services.Matching;

/// <summary>
/// Fuzzy string matching using multiple algorithms.
/// </summary>
/// <remarks>
/// Combines token matching, Levenshtein distance, and common OCR error handling.
/// </remarks>
public class FuzzyMatchingService : IFuzzyMatchingService
{
    // Common OCR substitution errors
    private static readonly Dictionary<char, char[]> OcrSubstitutions = new()
    {
        ['0'] = ['O', 'D', 'Q'],
        ['O'] = ['0', 'D', 'Q'],
        ['1'] = ['I', 'L', '|'],
        ['I'] = ['1', 'L', '|'],
        ['5'] = ['S'],
        ['S'] = ['5'],
        ['8'] = ['B'],
        ['B'] = ['8'],
        ['G'] = ['6'],
        ['2'] = ['Z'],
        ['Z'] = ['2'],
    };
    
    // Common Indonesian abbreviations
    private static readonly Dictionary<string, string[]> Abbreviations = new()
    {
        ["TPG"] = ["TEPUNG"],
        ["TRG"] = ["TERIGU"],
        ["GLR"] = ["GULA", "GULAR"],
        ["TLR"] = ["TELUR"],
        ["MNT"] = ["MENTEGA"],
        ["SKM"] = ["SUSU KENTAL MANIS"],
        ["KJU"] = ["KEJU"],
    };

    public MatchResult? FindBestMatch(
        string searchTerm, 
        IEnumerable<MatchCandidate> candidates)
    {
        var matches = FindAllMatches(searchTerm, candidates);
        return matches.FirstOrDefault();
    }

    public IReadOnlyList<MatchResult> FindAllMatches(
        string searchTerm, 
        IEnumerable<MatchCandidate> candidates,
        decimal minScore = 0.5m)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return [];
        
        var normalizedSearch = NormalizeText(searchTerm);
        
        var results = candidates
            .Select(candidate =>
            {
                var (score, matchType) = CalculateMatchWithType(
                    normalizedSearch, 
                    candidate);
                
                return new MatchResult(
                    CandidateId: candidate.Id,
                    CandidateText: candidate.Text,
                    Score: score,
                    MatchType: matchType
                );
            })
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .ToList();
        
        return results;
    }

    public decimal CalculateSimilarity(string a, string b)
    {
        var normA = NormalizeText(a);
        var normB = NormalizeText(b);
        
        if (normA == normB)
            return 1.0m;
        
        // Calculate Levenshtein similarity
        var distance = LevenshteinDistance(normA, normB);
        var maxLen = Math.Max(normA.Length, normB.Length);
        
        if (maxLen == 0)
            return 1.0m;
        
        return 1.0m - ((decimal)distance / maxLen);
    }

    private (decimal Score, string MatchType) CalculateMatchWithType(
        string search, 
        MatchCandidate candidate)
    {
        var candidateNorm = NormalizeText(candidate.Text);
        
        // Exact match
        if (search == candidateNorm)
            return (1.0m, "Exact");
        
        // Check aliases
        if (candidate.Aliases is not null)
        {
            foreach (var alias in candidate.Aliases)
            {
                if (NormalizeText(alias) == search)
                    return (0.95m, "Alias");
            }
        }
        
        // Contains match
        if (candidateNorm.Contains(search))
            return (0.85m, "Contains");
        
        if (search.Contains(candidateNorm))
            return (0.80m, "ContainsReverse");
        
        // Token matching
        var tokenScore = CalculateTokenMatch(search, candidateNorm);
        if (tokenScore >= 0.6m)
            return (tokenScore, "Token");
        
        // Levenshtein similarity
        var levScore = CalculateSimilarity(search, candidateNorm);
        if (levScore >= 0.5m)
            return (levScore, "Similarity");
        
        // OCR error correction
        var ocrCorrectedSearch = ApplyOcrCorrections(search);
        if (ocrCorrectedSearch != search)
        {
            var correctedScore = CalculateSimilarity(ocrCorrectedSearch, candidateNorm);
            if (correctedScore >= 0.7m)
                return (correctedScore * 0.9m, "OcrCorrected");
        }
        
        return (Math.Max(tokenScore, levScore), "Partial");
    }

    private static decimal CalculateTokenMatch(string search, string target)
    {
        var searchTokens = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var targetTokens = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (searchTokens.Length == 0 || targetTokens.Length == 0)
            return 0m;
        
        var matchCount = 0;
        
        foreach (var searchToken in searchTokens)
        {
            if (targetTokens.Any(t => 
                t.Contains(searchToken, StringComparison.OrdinalIgnoreCase) ||
                searchToken.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                matchCount++;
            }
        }
        
        return (decimal)matchCount / Math.Max(searchTokens.Length, targetTokens.Length);
    }

    private static string NormalizeText(string text)
    {
        // Uppercase
        var normalized = text.ToUpperInvariant();
        
        // Remove special characters except spaces
        normalized = new string(normalized
            .Where(c => char.IsLetterOrDigit(c) || c == ' ')
            .ToArray());
        
        // Collapse whitespace
        normalized = string.Join(' ', normalized.Split(' ', 
            StringSplitOptions.RemoveEmptyEntries));
        
        return normalized;
    }

    private static string ApplyOcrCorrections(string text)
    {
        var result = text;
        
        foreach (var (from, toOptions) in OcrSubstitutions)
        {
            // Try each substitution
            foreach (var to in toOptions)
            {
                result = result.Replace(from, to);
            }
        }
        
        return result;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a))
            return b?.Length ?? 0;
        
        if (string.IsNullOrEmpty(b))
            return a.Length;
        
        var matrix = new int[a.Length + 1, b.Length + 1];
        
        for (int i = 0; i <= a.Length; i++)
            matrix[i, 0] = i;
        
        for (int j = 0; j <= b.Length; j++)
            matrix[0, j] = j;
        
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }
        
        return matrix[a.Length, b.Length];
    }
}
```

### Register in Program.cs:

```csharp
using Nastart.Api.Shared.Services.Matching;

// Add fuzzy matching service
builder.Services.AddSingleton<IFuzzyMatchingService, FuzzyMatchingService>();
```

### Update ScanReceipt Handler to use FuzzyMatchingService:

```csharp
// In ScanReceiptHandler constructor, add:
private readonly IFuzzyMatchingService _fuzzyMatcher;

// Update FindBestMatch method:
private IngredientMatchResponse? FindBestMatch(
    string? itemName,
    List<IngredientInfo> ingredients)
{
    if (string.IsNullOrWhiteSpace(itemName))
        return null;
    
    var candidates = ingredients.Select(i => 
        new MatchCandidate(i.Id.ToString(), i.Name)).ToList();
    
    var match = _fuzzyMatcher.FindBestMatch(itemName, candidates);
    
    if (match is null)
        return null;
    
    var ingredient = ingredients.First(i => i.Id.ToString() == match.CandidateId);
    
    return new IngredientMatchResponse(
        IngredientId: ingredient.Id,
        IngredientName: ingredient.Name,
        Unit: ingredient.Unit,
        CurrentPrice: ingredient.CurrentPrice,
        MatchScore: match.Score
    );
}
```

### Your Task (Day 5):

1. Create `Shared/Services/Matching/` folder
2. Create `IFuzzyMatchingService.cs` interface
3. Create `FuzzyMatchingService.cs` implementation
4. Register in `Program.cs`
5. Update `ScanReceiptHandler` to use fuzzy matching
6. Verify build: `dotnet build`

---

# Day 6: Error Handling & Retry Policies

## 🧒 Explain Like I'm 5

Sometimes things go wrong:
- The OCR service is sleeping 😴 (timeout)
- The photo is too blurry 📸
- The internet hiccup 🌐

Instead of giving up, we:
1. Try again a few times
2. Tell the user nicely what went wrong
3. Keep working even if OCR is down

## 🔧 Engineer Language

**Resilience patterns** handle transient failures gracefully. We use Polly for retry policies and circuit breakers with the OCR client.

> 📖 **Microsoft Docs**: *"Use Microsoft.Extensions.Http.Resilience for resilient HTTP calls with retries, circuit breakers, and timeouts."*
>
> — [Build resilient HTTP apps](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)

### Install Resilience Packages:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

dotnet add package Microsoft.Extensions.Http.Resilience
```

### Configure Resilient HttpClient:

Update OCR service registration in `Program.cs`:

```csharp
using Microsoft.Extensions.Http.Resilience;
using Polly;

// Add typed HttpClient with resilience
builder.Services
    .AddHttpClient<IPaddleOcrService, PaddleOcrService>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<OcrServiceOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .AddStandardResilienceHandler(options =>
    {
        // Retry policy
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.UseJitter = true;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        
        // Circuit breaker
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        
        // Total timeout
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
    });
```

### Handle OCR Errors Gracefully:

Update `ScanReceiptHandler`:

```csharp
public async Task<Result<ScanReceiptResponse>> Handle(
    ScanReceiptCommand request, 
    CancellationToken cancellationToken)
{
    _logger.LogInformation(
        "Scanning receipt for user {UserId}", 
        request.UserId);
    
    OcrScanResult ocrResult;
    
    try
    {
        // Call OCR service with resilience
        ocrResult = await _ocrService.ScanReceiptAsync(
            request.ImageBase64, 
            cancellationToken);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
    {
        _logger.LogWarning("OCR service is unavailable");
        
        return Result<ScanReceiptResponse>.Failure(
            Error.Internal("OCR_SERVICE_UNAVAILABLE", 
                "Receipt scanning service is temporarily unavailable. Please try again later."));
    }
    catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
    {
        _logger.LogWarning("OCR service request timed out");
        
        return Result<ScanReceiptResponse>.Failure(
            Error.Internal("OCR_TIMEOUT", 
                "Receipt scanning took too long. Please try with a clearer image."));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during receipt scanning");
        
        return Result<ScanReceiptResponse>.Failure(
            Error.Internal("OCR_ERROR", 
                "Failed to process receipt. Please try again."));
    }
    
    // ... rest of handler
}
```

### Create Result Extensions:

Create `src/Nastart.Api/Shared/Models/ResultExtensions.cs`:

```csharp
namespace Nastart.Api.Shared.Models;

/// <summary>
/// Extension methods for Result type.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Maps result to different type if successful.
    /// </summary>
    public static Result<TOut> Map<TIn, TOut>(
        this Result<TIn> result, 
        Func<TIn, TOut> mapper)
    {
        return result.IsSuccess
            ? Result<TOut>.Success(mapper(result.Value!))
            : Result<TOut>.Failure(result.Error!);
    }
    
    /// <summary>
    /// Converts Result to IResult for Minimal API responses.
    /// </summary>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }
        
        return result.Error!.Type switch
        {
            "NotFound" => Results.NotFound(result.Error),
            "Validation" => Results.BadRequest(result.Error),
            "Conflict" => Results.Conflict(result.Error),
            "Unauthorized" => Results.Unauthorized(),
            _ => Results.Problem(
                detail: result.Error.Message,
                statusCode: 500)
        };
    }
}
```

### Your Task (Day 6):

1. Install `Microsoft.Extensions.Http.Resilience`
2. Update OCR client registration with resilience
3. Add error handling to `ScanReceiptHandler`
4. Create `ResultExtensions.cs`
5. Verify build: `dotnet build`

---

# Day 7: Testing with Real Receipts

## 🧒 Explain Like I'm 5

Now we test with REAL grocery receipts! 🧾
- Take photos of receipts from different stores
- See if OCR can read them
- Check if ingredients match correctly
- Fix any problems we find

## 🔧 Engineer Language

Integration testing with real receipt images validates the OCR pipeline end-to-end. We test various receipt formats, image qualities, and edge cases.

> 📖 **Microsoft Docs**: *"Integration tests verify that different parts of an application work correctly together, including external services."*
>
> — [Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)

### OCR Service Integration Test:

Create `tests/Nastart.Api.Tests/Features/Purchases/ScanReceiptTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Features.Purchases;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Services.Matching;
using Nastart.Api.Shared.Services.OcrService;

namespace Nastart.Api.Tests.Features.Purchases;

public class ScanReceiptTests
{
    private readonly Mock<IPaddleOcrService> _mockOcrService;
    private readonly Mock<ILogger<ScanReceiptHandler>> _mockLogger;
    private readonly NastartDbContext _db;
    private readonly IFuzzyMatchingService _fuzzyMatcher;
    private readonly ScanReceiptHandler _handler;

    public ScanReceiptTests()
    {
        _mockOcrService = new Mock<IPaddleOcrService>();
        _mockLogger = new Mock<ILogger<ScanReceiptHandler>>();
        _fuzzyMatcher = new FuzzyMatchingService();
        
        var options = new DbContextOptionsBuilder<NastartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        _db = new NastartDbContext(options);
        
        _handler = new ScanReceiptHandler(
            _mockOcrService.Object,
            _db,
            _fuzzyMatcher,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_SuccessfulOcr_ReturnsMatchedItems()
    {
        // Arrange
        var userId = Guid.NewGuid();
        
        // Seed test ingredients
        _db.Ingredients.AddRange(
            new Ingredient
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Tepung Terigu",
                Unit = "kg",
                CurrentPrice = 18000,
                CurrentStock = 10,
                MinimumStock = 2,
                CreatedAt = DateTime.UtcNow
            },
            new Ingredient
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Gula Pasir",
                Unit = "kg",
                CurrentPrice = 16000,
                CurrentStock = 5,
                MinimumStock = 1,
                CreatedAt = DateTime.UtcNow
            }
        );
        await _db.SaveChangesAsync();
        
        // Mock OCR response
        _mockOcrService
            .Setup(s => s.ScanReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrScanResult
            {
                Status = OcrStatus.Success,
                ReceiptLines = new List<OcrReceiptLine>
                {
                    new()
                    {
                        RawText = "TEPUNG TERIGU 2KG 36.000",
                        ItemName = "TEPUNG TERIGU",
                        Quantity = 2,
                        Unit = "KG",
                        Price = 36000,
                        Confidence = 0.95m
                    },
                    new()
                    {
                        RawText = "GULA PASIR 1KG 16.000",
                        ItemName = "GULA PASIR",
                        Quantity = 1,
                        Unit = "KG",
                        Price = 16000,
                        Confidence = 0.92m
                    }
                },
                ShopName = "SUPERINDO",
                TotalDetected = 52000,
                ProcessingTimeMs = 1500
            });
        
        var command = new ScanReceiptCommand(
            UserId: userId,
            ImageBase64: "SGVsbG8gV29ybGQ="); // dummy base64
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items[0].SuggestedMatch.Should().NotBeNull();
        result.Value.Items[0].SuggestedMatch!.IngredientName.Should().Be("Tepung Terigu");
        result.Value.Status.Should().Be(ScanStatus.Success);
    }

    [Fact]
    public async Task Handle_OcrFailed_ReturnsFailedStatus()
    {
        // Arrange
        _mockOcrService
            .Setup(s => s.ScanReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrScanResult
            {
                Status = OcrStatus.Failed,
                ErrorMessage = "No text detected",
                ProcessingTimeMs = 500
            });
        
        var command = new ScanReceiptCommand(
            UserId: Guid.NewGuid(),
            ImageBase64: "SGVsbG8gV29ybGQ=");
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue(); // Still returns success with status
        result.Value!.Status.Should().Be(ScanStatus.OcrFailed);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NoMatchingIngredients_ReturnsPartialSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        // No ingredients seeded for this user
        
        _mockOcrService
            .Setup(s => s.ScanReceiptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OcrScanResult
            {
                Status = OcrStatus.Success,
                ReceiptLines = new List<OcrReceiptLine>
                {
                    new()
                    {
                        RawText = "UNKNOWN ITEM 1KG 10.000",
                        ItemName = "UNKNOWN ITEM",
                        Quantity = 1,
                        Unit = "KG",
                        Price = 10000,
                        Confidence = 0.90m
                    }
                },
                ProcessingTimeMs = 1000
            });
        
        var command = new ScanReceiptCommand(
            UserId: userId,
            ImageBase64: "SGVsbG8gV29ybGQ=");
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ScanStatus.PartialSuccess);
        result.Value.Items[0].SuggestedMatch.Should().BeNull();
        result.Value.UnmatchedLines.Should().NotBeEmpty();
    }
}
```

### FuzzyMatchingService Tests:

Create `tests/Nastart.Api.Tests/Shared/Services/FuzzyMatchingServiceTests.cs`:

```csharp
using FluentAssertions;
using Nastart.Api.Shared.Services.Matching;

namespace Nastart.Api.Tests.Shared.Services;

public class FuzzyMatchingServiceTests
{
    private readonly FuzzyMatchingService _service = new();

    [Theory]
    [InlineData("TEPUNG TERIGU", "Tepung Terigu", 1.0)]
    [InlineData("TEPUNG", "Tepung Terigu", 0.8)] // Contains
    [InlineData("TPNG TRGU", "Tepung Terigu", 0.5)] // Partial match
    public void FindBestMatch_VariousInputs_ReturnsExpectedScore(
        string search, 
        string candidate, 
        decimal minScore)
    {
        // Arrange
        var candidates = new[] { new MatchCandidate("1", candidate) };
        
        // Act
        var result = _service.FindBestMatch(search, candidates);
        
        // Assert
        result.Should().NotBeNull();
        result!.Score.Should().BeGreaterOrEqualTo(minScore);
    }

    [Fact]
    public void FindBestMatch_MultipleCandidate_ReturnsBestMatch()
    {
        // Arrange
        var candidates = new[]
        {
            new MatchCandidate("1", "Tepung Terigu"),
            new MatchCandidate("2", "Tepung Beras"),
            new MatchCandidate("3", "Gula Pasir")
        };
        
        // Act
        var result = _service.FindBestMatch("TEPUNG TERIGU", candidates);
        
        // Assert
        result.Should().NotBeNull();
        result!.CandidateId.Should().Be("1");
        result.Score.Should().BeGreaterThan(0.9m);
    }

    [Fact]
    public void FindBestMatch_NoMatch_ReturnsNull()
    {
        // Arrange
        var candidates = new[]
        {
            new MatchCandidate("1", "Tepung Terigu"),
            new MatchCandidate("2", "Gula Pasir")
        };
        
        // Act
        var result = _service.FindBestMatch("XYZ UNKNOWN ITEM", candidates);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CalculateSimilarity_IdenticalStrings_ReturnsOne()
    {
        // Act
        var score = _service.CalculateSimilarity("Hello", "Hello");
        
        // Assert
        score.Should().Be(1.0m);
    }

    [Fact]
    public void CalculateSimilarity_CompletelyDifferent_ReturnsLowScore()
    {
        // Act
        var score = _service.CalculateSimilarity("ABC", "XYZ");
        
        // Assert
        score.Should().BeLessThan(0.5m);
    }
}
```

### Manual Testing with Real Receipts:

```powershell
# Start all services
cd C:\Users\AU1833\Documents\personal\nastart
docker compose up -d

# Test OCR service directly
$body = @{
    image_base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes("path/to/receipt.jpg"))
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:8001/scan" -Method Post -Body $body -ContentType "application/json"

# Test via .NET API
# POST /api/purchases/scan with image_base64 in body
```

### Run Tests:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

dotnet test --filter "FullyQualifiedName~ScanReceipt"
dotnet test --filter "FullyQualifiedName~FuzzyMatching"
```

### Your Task (Day 7):

1. Create `ScanReceiptTests.cs`
2. Create `FuzzyMatchingServiceTests.cs`
3. Run tests: `dotnet test`
4. Test with real receipt images
5. Document any edge cases found

---

# Resources

## Microsoft Official Documentation

| Topic | Link |
|-------|------|
| **Microservices** | [learn.microsoft.com/dotnet/architecture/microservices](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/) |
| **IHttpClientFactory** | [learn.microsoft.com/dotnet/core/extensions/httpclient-factory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory) |
| **HTTP Resilience** | [learn.microsoft.com/dotnet/core/resilience/http-resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience) |
| **Minimal APIs** | [learn.microsoft.com/aspnet/core/fundamentals/minimal-apis](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview) |
| **Integration Testing** | [learn.microsoft.com/aspnet/core/test/integration-tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) |
| **String Best Practices** | [learn.microsoft.com/dotnet/standard/base-types/best-practices-strings](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings) |

## External Resources

| Topic | Link |
|-------|------|
| **PaddleOCR** | [github.com/PaddlePaddle/PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR) |
| **FastAPI** | [fastapi.tiangolo.com](https://fastapi.tiangolo.com/) |

## Week 6 Checklist

- [ ] Created `ocr-service/` folder structure
- [ ] Created `requirements.txt` with PaddleOCR dependencies
- [ ] Created Pydantic models (`models.py`)
- [ ] Created PaddleOCR wrapper (`ocr.py`)
- [ ] Created receipt parsing logic (`preprocessing.py`)
- [ ] Created FastAPI app (`main.py`)
- [ ] Created Dockerfile for OCR service
- [ ] Updated `docker-compose.yml`
- [ ] Created `IPaddleOcrService` interface
- [ ] Created `PaddleOcrService` HTTP client
- [ ] Created `OcrModels.cs` DTOs
- [ ] Created `ScanReceipt` feature slice
- [ ] Created `IFuzzyMatchingService` interface
- [ ] Created `FuzzyMatchingService` implementation
- [ ] Added HTTP resilience with retry policies
- [ ] Created integration tests
- [ ] Tested with real receipt images
- [ ] All builds pass: `dotnet build`
- [ ] All tests pass: `dotnet test`

---

## What's Next?

**Week 7: API Polish & OpenAPI** — Configure OpenAPI/Swagger documentation, add request/response examples, configure CORS, add health check endpoints, rate limiting, and error handling middleware.

---

*Nastart — Start smart, bake profitable*
