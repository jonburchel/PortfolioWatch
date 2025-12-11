import requests
import json

def test_lookup_symbol(cusip, company_name):
    url = "https://portfolio-watch-eghnhrb0fgd0gyb3.centralus-01.azurewebsites.net/lookup-symbol"
    payload = {
        "Cusip": cusip,
        "CompanyName": company_name
    }
    
    headers = {
        "Content-Type": "application/json"
    }
    
    print(f"Testing lookup for CUSIP: {cusip}, Company: {company_name}")
    print(f"URL: {url}")
    
    try:
        response = requests.post(url, json=payload, headers=headers)
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            print("Response Body:")
            print(response.text)
            
            try:
                data = response.json()
                if "text" in data:
                    print(f"Found 'text': {data['text']}")
                elif "Text" in data:
                    print(f"Found 'Text': {data['Text']}")
                else:
                    print("Neither 'text' nor 'Text' property found in JSON.")
            except json.JSONDecodeError:
                print("Failed to parse JSON response.")
        else:
            print(f"Error: {response.text}")
            
    except Exception as e:
        print(f"Exception occurred: {e}")

if __name__ == "__main__":
    # Test with Apple's CUSIP
    test_lookup_symbol("31617E471", "FID GR CO POOL CL S")
    
    print("-" * 20)
    
    # Test with a random/invalid CUSIP to see behavior
    test_lookup_symbol("000000000", "Fake Company")
